using Adapters.Abstractions;
using Contracts;

namespace Diagnostic.Core;

/// <summary>
/// Device discovery, connection and disconnection over one <see cref="IVciAdapter"/>.
/// Discovery never connects. All adapter calls are serialized.
/// </summary>
/// <remarks>
/// Awaits here use ConfigureAwait(false): a desktop application may block its UI thread while it closes
/// the engine, and the connection gate must still be released. Adapters should do the same.
/// </remarks>
public sealed class VciConnectionManager : IAsyncDisposable
{
    private readonly IVciAdapter _adapter;
    private readonly DiagnosticOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private AdapterInfo? _connected;
    private bool _simulated;
    private long _generation;

    public VciConnectionManager(IVciAdapter adapter, DiagnosticOptions? options = null, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        _adapter = adapter;
        _options = options ?? new DiagnosticOptions();
        Time = timeProvider ?? TimeProvider.System;
    }

    public bool IsConnected => _connected is not null && _adapter.IsConnected;

    public AdapterInfo? ConnectedAdapter => IsConnected ? _connected : null;

    internal TimeProvider Time { get; }

    /// <summary>Changes on every new connection so that old sessions cannot reuse it.</summary>
    internal long Generation => Interlocked.Read(ref _generation);

    /// <summary>An empty list with Success means no device was found.</summary>
    public Task<OperationResult<IReadOnlyList<AdapterInfo>>> DiscoverAsync(CancellationToken ct) =>
        RunExclusiveAsync(DiscoverCoreAsync, ct);

    /// <summary>Succeeds without reconnecting when already connected to the same adapter.</summary>
    public Task<OperationResult<AdapterInfo>> ConnectAsync(string adapterId, CancellationToken ct) =>
        RunExclusiveAsync(token => ConnectCoreAsync(adapterId, token), ct);

    public Task<OperationResult<bool>> DisconnectAsync(CancellationToken ct) =>
        RunExclusiveAsync(DisconnectCoreAsync, ct);

    internal Task<OperationResult<byte[]>> SendAsync(byte[] request, long generation, CancellationToken ct) =>
        RunExclusiveAsync(
            async token =>
            {
                if (!IsConnected || generation != Generation)
                {
                    return Results.Fail<byte[]>(
                        OperationStatus.NotConnected,
                        "NotConnected",
                        "The adapter connection for this session is no longer open.",
                        Time,
                        _simulated);
                }

                return await AdapterGuard.ExecuteAsync(
                    t => _adapter.SendAsync(request, _options.RequestTimeout, t),
                    _options.RequestTimeout + _options.AdapterGracePeriod,
                    Time,
                    _simulated,
                    token).ConfigureAwait(false);
            },
            ct);

    public async ValueTask DisposeAsync()
    {
        _connected = null;
        await _adapter.DisposeAsync().ConfigureAwait(false);
    }

    // Temporary decision (AdapterInfo cannot express simulation): a simulated adapter says
    // SIMULATED in its display name. Connect and send results also carry IsSimulated.
    private static bool IsSimulatedAdapter(AdapterInfo adapter) =>
        adapter.DisplayName.Contains("SIMULATED", StringComparison.OrdinalIgnoreCase);

    private async Task<OperationResult<T>> RunExclusiveAsync<T>(
        Func<CancellationToken, Task<OperationResult<T>>> action, CancellationToken ct)
    {
        try
        {
            await _gate.WaitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return Results.Cancelled<T>(Time, _simulated);
        }

        try
        {
            return await action(ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private Task<OperationResult<IReadOnlyList<AdapterInfo>>> DiscoverCoreAsync(CancellationToken ct) =>
        AdapterGuard.ExecuteAsync<IReadOnlyList<AdapterInfo>>(
            async token =>
            {
                var adapters = await _adapter.DiscoverAsync(token).ConfigureAwait(false);
                return new OperationResult<IReadOnlyList<AdapterInfo>>(
                    OperationStatus.Success,
                    adapters,
                    null,
                    null,
                    Time.GetUtcNow(),
                    adapters.Count > 0 && adapters.All(IsSimulatedAdapter));
            },
            _options.ConnectionTimeout,
            Time,
            false,
            ct);

    private async Task<OperationResult<AdapterInfo>> ConnectCoreAsync(string adapterId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(adapterId))
        {
            return Results.Fail<AdapterInfo>(
                OperationStatus.Failed, "InvalidArgument", "An adapter id is required.", Time, false);
        }

        if (IsConnected)
        {
            if (_connected!.AdapterId != adapterId)
            {
                return Results.Fail<AdapterInfo>(
                    OperationStatus.Failed, "AdapterBusy", "Disconnect the current adapter first.", Time, _simulated);
            }

            return new OperationResult<AdapterInfo>(
                OperationStatus.Success, _connected, "Already connected.", null, Time.GetUtcNow(), _simulated);
        }

        _connected = null;
        var discovery = await DiscoverCoreAsync(ct).ConfigureAwait(false);
        if (discovery.Data is null)
        {
            return Results.Fail<AdapterInfo>(
                discovery.Status,
                discovery.ErrorCode ?? "DiscoveryFailed",
                discovery.Message ?? "Adapter discovery failed.",
                Time,
                false);
        }

        var info = discovery.Data.FirstOrDefault(a => a.AdapterId == adapterId);
        if (info is null)
        {
            return Results.Fail<AdapterInfo>(
                OperationStatus.Failed, "AdapterNotFound", "No adapter with this id was found.", Time, false);
        }

        var looksSimulated = IsSimulatedAdapter(info);
        var connect = await AdapterGuard.ExecuteAsync(
            token => _adapter.ConnectAsync(adapterId, token),
            _options.ConnectionTimeout,
            Time,
            looksSimulated,
            ct).ConfigureAwait(false);

        if (connect.Status != OperationStatus.Success)
        {
            if (connect.Status is OperationStatus.Timeout or OperationStatus.Cancelled)
            {
                // Release whatever a late or half-finished connection may hold.
                await DisconnectCoreAsync(CancellationToken.None).ConfigureAwait(false);
            }

            return new OperationResult<AdapterInfo>(
                connect.Status, null, connect.Message, connect.ErrorCode, connect.TimestampUtc, connect.IsSimulated);
        }

        _connected = info;
        _simulated = connect.IsSimulated || looksSimulated;
        Interlocked.Increment(ref _generation);
        return new OperationResult<AdapterInfo>(
            OperationStatus.Success, info, null, null, connect.TimestampUtc, _simulated);
    }

    private async Task<OperationResult<bool>> DisconnectCoreAsync(CancellationToken ct)
    {
        var result = await AdapterGuard.ExecuteAsync(
            token => _adapter.DisconnectAsync(token),
            _options.ConnectionTimeout,
            Time,
            _simulated,
            ct).ConfigureAwait(false);

        if (result.Status == OperationStatus.Success || !_adapter.IsConnected)
        {
            _connected = null;
        }

        return result;
    }
}

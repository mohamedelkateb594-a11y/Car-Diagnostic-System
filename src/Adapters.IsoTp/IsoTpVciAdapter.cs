using Adapters.Abstractions;
using Contracts;
using Diagnostic.Protocols;

namespace Adapters.IsoTp;

/// <summary>
/// <see cref="IVciAdapter"/> over a frame-level <see cref="ICanChannel"/> using ISO-TP.
/// It reports transport outcomes only; the protocol layer interprets ECU replies.
/// </summary>
public sealed class IsoTpVciAdapter : IVciAdapter
{
    private readonly AdapterInfo _info;
    private readonly Func<ICanChannel> _openChannel;
    private readonly IsoTpOptions _options;
    private readonly bool _simulated;
    private readonly TimeProvider _time;
    private ICanChannel? _channel;
    private IsoTpTransport? _transport;
    private bool _disposed;

    /// <param name="openChannel">Opens the channel on connect. A disposable channel is disposed on disconnect.</param>
    public IsoTpVciAdapter(
        AdapterInfo info,
        Func<ICanChannel> openChannel,
        IsoTpOptions options,
        bool isSimulated,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(openChannel);
        ArgumentNullException.ThrowIfNull(options);
        _info = info;
        _openChannel = openChannel;
        _options = options;
        _simulated = isSimulated;
        _time = timeProvider ?? TimeProvider.System;
    }

    public bool IsConnected => _transport is not null;

    public Task<IReadOnlyList<AdapterInfo>> DiscoverAsync(CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            return Task.FromCanceled<IReadOnlyList<AdapterInfo>>(ct);
        }

        IReadOnlyList<AdapterInfo> adapters = _disposed ? [] : [_info];
        return Task.FromResult(adapters);
    }

    public async Task<OperationResult<bool>> ConnectAsync(string adapterId, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            return Result(OperationStatus.Cancelled, false, "Connection cancelled.", "Cancelled");
        }

        if (_disposed || adapterId != _info.AdapterId)
        {
            return Result(OperationStatus.Failed, false, "Unknown adapter.", "AdapterNotFound");
        }

        if (_transport is not null)
        {
            return Result(OperationStatus.Failed, false, "The adapter is already connected.", "AdapterBusy");
        }

        ICanChannel? channel = null;
        try
        {
            channel = _openChannel();
            _transport = new IsoTpTransport(channel, _options, _time);
            _channel = channel;
            return Result(OperationStatus.Success, true, null, null);
        }
        catch (Exception ex)
        {
            await ReleaseAsync(channel).ConfigureAwait(false);
            return Result(
                OperationStatus.TransportError, false, $"{ex.GetType().Name}: {ex.Message}", "ChannelOpenFailed");
        }
    }

    public async Task<OperationResult<bool>> DisconnectAsync(CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            return Result(OperationStatus.Cancelled, false, "Disconnection cancelled.", "Cancelled");
        }

        try
        {
            await CloseAsync().ConfigureAwait(false);
            return Result(OperationStatus.Success, true, null, null);
        }
        catch (Exception ex)
        {
            return Result(
                OperationStatus.TransportError, false, $"{ex.GetType().Name}: {ex.Message}", "ChannelCloseFailed");
        }
    }

    public async Task<OperationResult<byte[]>> SendAsync(byte[] request, TimeSpan timeout, CancellationToken ct)
    {
        var transport = _transport;
        if (transport is null)
        {
            return Result<byte[]>(OperationStatus.NotConnected, null, "No channel is open.", "NotConnected");
        }

        if (request is null)
        {
            return Result<byte[]>(OperationStatus.Failed, null, "A request is required.", "InvalidArgument");
        }

        var exchange = await transport.ExchangeAsync(request, timeout, ct).ConfigureAwait(false);
        return Result(exchange.Status, exchange.Value, exchange.Message, exchange.ErrorCode);
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        await CloseAsync().ConfigureAwait(false);
    }

    private static async ValueTask ReleaseAsync(ICanChannel? channel)
    {
        switch (channel)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }
    }

    private async ValueTask CloseAsync()
    {
        var channel = _channel;
        _channel = null;
        _transport = null;
        await ReleaseAsync(channel).ConfigureAwait(false);
    }

    private OperationResult<T> Result<T>(OperationStatus status, T? data, string? message, string? errorCode) =>
        new(status, data, message, errorCode, _time.GetUtcNow(), _simulated);
}

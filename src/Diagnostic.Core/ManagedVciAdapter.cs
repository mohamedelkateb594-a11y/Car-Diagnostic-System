using Adapters.Abstractions;
using Contracts;

namespace Diagnostic.Core;

/// <summary>
/// <see cref="IVciAdapter"/> view of a <see cref="VciConnectionManager"/> for consumers that connect
/// through the shared abstraction, such as the UI. <see cref="DiagnosticService"/> then uses the same
/// connection. A consumer that connects the raw adapter instead makes the service connect it a second time.
/// </summary>
public sealed class ManagedVciAdapter : IVciAdapter
{
    private readonly VciConnectionManager _connection;

    public ManagedVciAdapter(VciConnectionManager connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;
    }

    public bool IsConnected => _connection.IsConnected;

    /// <summary>
    /// The shared signature carries no status, so a failed discovery throws instead of returning an
    /// empty list that would read as "no device".
    /// </summary>
    public async Task<IReadOnlyList<AdapterInfo>> DiscoverAsync(CancellationToken ct)
    {
        var result = await _connection.DiscoverAsync(ct).ConfigureAwait(false);
        if (result.Data is not null)
        {
            return result.Data;
        }

        ct.ThrowIfCancellationRequested();
        throw new InvalidOperationException($"{result.Status}: {result.Message}");
    }

    public async Task<OperationResult<bool>> ConnectAsync(string adapterId, CancellationToken ct)
    {
        var result = await _connection.ConnectAsync(adapterId, ct).ConfigureAwait(false);
        return new OperationResult<bool>(
            result.Status,
            result.Status == OperationStatus.Success,
            result.Message,
            result.ErrorCode,
            result.TimestampUtc,
            result.IsSimulated);
    }

    public Task<OperationResult<bool>> DisconnectAsync(CancellationToken ct) => _connection.DisconnectAsync(ct);

    /// <summary>Always refused: requests go through the diagnostic core, which only sends read services.</summary>
    public Task<OperationResult<byte[]>> SendAsync(byte[] request, TimeSpan timeout, CancellationToken ct)
    {
        var simulated = _connection.ConnectedAdapter?.DisplayName
            .Contains("SIMULATED", StringComparison.OrdinalIgnoreCase) ?? false;
        return Task.FromResult(Results.Fail<byte[]>(
            OperationStatus.Unsupported,
            "RawAccessNotAllowed",
            "Use IDiagnosticService for diagnostic requests.",
            _connection.Time,
            simulated));
    }

    /// <summary>Releases the connection. The manager keeps ownership of the underlying adapter.</summary>
    public async ValueTask DisposeAsync() =>
        await _connection.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
}

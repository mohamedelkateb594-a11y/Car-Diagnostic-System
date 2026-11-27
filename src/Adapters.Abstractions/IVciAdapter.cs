using Contracts;

namespace Adapters.Abstractions;

public interface IVciAdapter : IAsyncDisposable
{
    Task<IReadOnlyList<AdapterInfo>> DiscoverAsync(CancellationToken ct);
    Task<OperationResult<bool>> ConnectAsync(string adapterId, CancellationToken ct);
    Task<OperationResult<bool>> DisconnectAsync(CancellationToken ct);
    Task<OperationResult<byte[]>> SendAsync(
        byte[] request,
        TimeSpan timeout,
        CancellationToken ct);
    bool IsConnected { get; }
}

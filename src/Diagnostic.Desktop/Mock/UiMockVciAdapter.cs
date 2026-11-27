using Adapters.Abstractions;
using Contracts;

namespace Diagnostic.Desktop.Mock;

/// <summary>
/// TEMPORARY simulated VCI adapter for UI development. No hardware, no I/O.
/// Discovery never connects; connecting is an explicit call (API_CONTRACTS.md section 3).
/// </summary>
public sealed class UiMockVciAdapter : IVciAdapter
{
    public const string AdapterId = "ui-mock-vci-1";

    public static readonly AdapterInfo Info = new(
        AdapterId,
        "Simulated VCI (UI mock)",
        "UI mock",
        "SIM",
        "0.0-sim",
        new[] { "SIMULATED" });

    public bool IsConnected { get; private set; }

    public Task<IReadOnlyList<AdapterInfo>> DiscoverAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<AdapterInfo>>(new[] { Info });

    public Task<OperationResult<bool>> ConnectAsync(string adapterId, CancellationToken ct)
    {
        if (adapterId != AdapterId)
        {
            return Task.FromResult(MockResult.Fail<bool>(
                OperationStatus.NotApplicable, "Unknown adapter id.", "UNKNOWN_ADAPTER"));
        }

        IsConnected = true;
        return Task.FromResult(MockResult.Ok(true));
    }

    public Task<OperationResult<bool>> DisconnectAsync(CancellationToken ct)
    {
        IsConnected = false;
        return Task.FromResult(MockResult.Ok(true));
    }

    public Task<OperationResult<byte[]>> SendAsync(byte[] request, TimeSpan timeout, CancellationToken ct)
        => Task.FromResult(MockResult.Fail<byte[]>(
            OperationStatus.Unsupported, "The UI mock adapter does not transport frames.", "MOCK_NO_TRANSPORT"));

    public ValueTask DisposeAsync()
    {
        IsConnected = false;
        return ValueTask.CompletedTask;
    }
}

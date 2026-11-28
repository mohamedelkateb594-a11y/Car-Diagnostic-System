using Contracts;
using Diagnostic.Core;

namespace Adapters.Mock;

/// <summary>
/// Ready-to-use SIMULATED diagnostic service: the real Core running over <see cref="MockVciAdapter"/>.
/// Register it as <see cref="IDiagnosticService"/> and expose <see cref="Connection"/> for discovery.
/// </summary>
public sealed class MockDiagnosticService : IDiagnosticService, IAsyncDisposable
{
    private readonly DiagnosticService _service;

    public MockDiagnosticService(MockVciAdapter? adapter = null, DiagnosticOptions? options = null)
    {
        Adapter = adapter ?? new MockVciAdapter();
        Connection = new VciConnectionManager(Adapter, options);
        _service = new DiagnosticService(Connection);
    }

    public MockVciAdapter Adapter { get; }

    public VciConnectionManager Connection { get; }

    public Task<OperationResult<DiagnosticSessionInfo>> StartSessionAsync(
        string adapterId, VehicleIdentity? manualVehicle, CancellationToken ct) =>
        _service.StartSessionAsync(adapterId, manualVehicle, ct);

    public Task<OperationResult<bool>> EndSessionAsync(Guid sessionId, CancellationToken ct) =>
        _service.EndSessionAsync(sessionId, ct);

    public Task<OperationResult<VehicleIdentity>> ReadVehicleIdentityAsync(Guid sessionId, CancellationToken ct) =>
        _service.ReadVehicleIdentityAsync(sessionId, ct);

    public Task<OperationResult<IReadOnlyList<EcuScanResult>>> FullScanAsync(Guid sessionId, CancellationToken ct) =>
        _service.FullScanAsync(sessionId, ct);

    public Task<OperationResult<IReadOnlyList<DtcRecord>>> ReadDtcsAsync(
        Guid sessionId, string ecuId, CancellationToken ct) =>
        _service.ReadDtcsAsync(sessionId, ecuId, ct);

    public Task<OperationResult<IReadOnlyList<SupportedParameter>>> GetSupportedParametersAsync(
        Guid sessionId, string ecuId, CancellationToken ct) =>
        _service.GetSupportedParametersAsync(sessionId, ecuId, ct);

    public Task<OperationResult<IReadOnlyList<LiveDataPoint>>> ReadLiveDataAsync(
        Guid sessionId, string ecuId, IReadOnlyList<string> parameterIds, CancellationToken ct) =>
        _service.ReadLiveDataAsync(sessionId, ecuId, parameterIds, ct);

    public Task<OperationResult<FreezeFrameRecord?>> ReadFreezeFrameAsync(
        Guid sessionId, string ecuId, string? dtcCode, CancellationToken ct) =>
        _service.ReadFreezeFrameAsync(sessionId, ecuId, dtcCode, ct);

    public ValueTask DisposeAsync() => Connection.DisposeAsync();
}

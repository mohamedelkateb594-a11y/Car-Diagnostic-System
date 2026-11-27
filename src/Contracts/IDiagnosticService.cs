namespace Contracts;

public interface IDiagnosticService
{
    Task<OperationResult<DiagnosticSessionInfo>> StartSessionAsync(
        string adapterId, VehicleIdentity? manualVehicle, CancellationToken ct);

    Task<OperationResult<VehicleIdentity>> ReadVehicleIdentityAsync(
        Guid sessionId, CancellationToken ct);

    Task<OperationResult<IReadOnlyList<EcuScanResult>>> FullScanAsync(
        Guid sessionId, CancellationToken ct);

    Task<OperationResult<IReadOnlyList<DtcRecord>>> ReadDtcsAsync(
        Guid sessionId, string ecuId, CancellationToken ct);

    Task<OperationResult<IReadOnlyList<LiveDataPoint>>> ReadLiveDataAsync(
        Guid sessionId, string ecuId, IReadOnlyList<string> parameterIds,
        CancellationToken ct);

    Task<OperationResult<FreezeFrameRecord?>> ReadFreezeFrameAsync(
        Guid sessionId, string ecuId, string? dtcCode, CancellationToken ct);
}

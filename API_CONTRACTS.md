# API Contracts

> هذه الوثيقة هي اتفاق التكامل بين المطورين. لا تغيّر أي contract دون PR وموافقة بقية الفريق.

## 1. General conventions
- C# interfaces and DTOs live in `src/Contracts`, except the VCI abstraction `IVciAdapter`, which lives in `src/Adapters.Abstractions` and depends on `Contracts`.
- Async I/O methods return `Task<T>` and accept `CancellationToken`.
- Use nullable reference types.
- Use `DateTimeOffset` in UTC for timestamps.
- Units must be explicit (e.g. `rpm`, `degC`, `kPa`, `V`, `%`).
- Never represent missing/unsupported values as zero.
- Simulated results must carry `IsSimulated = true`.
- All operations return structured results; do not use exceptions for expected outcomes such as timeout or unsupported service.
- Consumers check `Status` before reading `Data`. For value types (for example `OperationResult<bool>`), `Data` holds the type default on failure and carries no meaning.
- `PartialSuccess` means `Data` is present but some requested items are missing.
- `PermissionDenied` and `UnknownError` (ARCHITECTURE.md error model) are reported as `Status = Failed` with `ErrorCode` set to that name.

## 2. Shared result types
Suggested initial contracts (names may be refined by an approved PR):

```csharp
public enum OperationStatus
{
    Success, PartialSuccess, Unsupported, NotApplicable,
    NotConnected, Timeout, Cancelled, InvalidResponse,
    TransportError, ProtocolError, SafetyPreconditionFailed,
    Failed
}

public sealed record OperationResult<T>(
    OperationStatus Status,
    T? Data,
    string? Message,
    string? ErrorCode,
    DateTimeOffset TimestampUtc,
    bool IsSimulated);

public sealed record VehicleIdentity(
    string? Vin,
    string? Make,
    string? Model,
    int? ModelYear,
    string? Engine,
    string? Market);

public sealed record AdapterInfo(
    string AdapterId,
    string DisplayName,
    string Manufacturer,
    string Model,
    string FirmwareVersion,
    IReadOnlyList<string> SupportedProtocols);

public sealed record DiagnosticSessionInfo(
    Guid SessionId,
    VehicleIdentity? Vehicle,
    AdapterInfo Adapter,
    DateTimeOffset StartedAtUtc,
    bool IsSimulated);
```

## 3. Adapter abstraction
Initial interface proposal:

```csharp
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
```

Rules:
- `SendAsync` is a low-level transport contract, not permission to send arbitrary frames.
- Protocol layer validates message format and supported service.
- Hardware implementation must document driver/SDK/license requirements.
- Adapter discovery must not connect automatically without user intent.
- Disconnection and cancellation must release resources safely.
- Phase 1 payload level: `request` is a diagnostic service payload (service identifier and parameters) and `Data` is the service response payload. Neither contains CAN headers or ISO-TP control bytes; transport framing is the adapter's job.
- Phase 1 addresses a single simulated ECU. ECU addressing, multiple responses and frame-level transport require a separate contract change.
- Until `AdapterInfo` can express simulation, a simulated adapter includes `SIMULATED` in `DisplayName`.

## 4. Diagnostic service
```csharp
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
```

## 5. Diagnostic DTOs
```csharp
public enum EcuScanStatus
{
    Responded, NoResponse, Unsupported, NotAttempted, Error
}

public sealed record EcuScanResult(
    string EcuId,
    string DisplayName,
    EcuScanStatus Status,
    IReadOnlyList<DtcRecord> Dtcs,
    string? Notes);

public sealed record DtcRecord(
    string Code,
    string? Description,
    string? Status,
    string? EcuId,
    string? SourceId,
    bool IsConfirmed,
    bool IsPending);

public sealed record LiveDataPoint(
    string ParameterId,
    string Name,
    double Value,
    string Unit,
    DateTimeOffset TimestampUtc,
    bool IsSimulated);

public sealed record FreezeFrameRecord(
    string? DtcCode,
    IReadOnlyList<LiveDataPoint> Values,
    DateTimeOffset? CapturedAtUtc);
```

Rules:
- A requested live-data parameter that is unavailable is omitted (never zero) and the result is `PartialSuccess`.
- In freeze-frame values, `TimestampUtc` is the time the value was read.
- `Success` with null `Data` from `ReadFreezeFrameAsync` means no freeze frame is stored.
- A failed DTC read for an ECU is reported as `EcuScanStatus.Error` with `Notes`. `Responded` with an empty `Dtcs` list means the ECU reported no codes.

## 6. Vehicle definition repository
```csharp
public interface IVehicleDefinitionRepository
{
    Task<IReadOnlyList<VehicleMatch>> FindByVinAsync(string vin, CancellationToken ct);
    Task<IReadOnlyList<VehicleMatch>> SearchAsync(
        string? make, string? model, int? year, string? engine,
        CancellationToken ct);
    Task<IReadOnlyList<EcuDefinition>> GetEcusAsync(
        string vehicleDefinitionId, CancellationToken ct);
    Task<IReadOnlyList<ParameterDefinition>> GetParametersAsync(
        string ecuDefinitionId, CancellationToken ct);
    Task<DataSourceInfo?> GetSourceAsync(string sourceId, CancellationToken ct);
}
```

Suggested metadata:
- `VehicleMatch`: definition ID, make/model/year/engine/market, confidence and match basis.
- `EcuDefinition`: ID, display name, system, applicability, supported protocol/service references.
- `ParameterDefinition`: ID, name, unit, description, decoding metadata, applicability.
- `DataSourceInfo`: source name/URL, license identifier, attribution, collected date, version, review status.

## 7. Capability and applicability
Before offering an operation, resolve:
- Adapter capability.
- Protocol support.
- Vehicle/ECU applicability.
- Data definition availability.
- Required operating preconditions.
Unknown must not be treated as supported.

Suggested enum:
```csharp
public enum SupportLevel { Supported, Unsupported, Unknown, NotTested }
```

## 8. Safety boundary
The first release supports read-oriented operations only. The initial contract has no Clear DTC operation. Clear DTC is state-changing; adding it requires a separate reviewed contract with an explicit confirmation flow in UI and a Core-side guard. Active tests, coding, adaptation, security access and ECU programming are not part of the initial contract. They require a separate reviewed contract and safety design.

## 9. Reporting contract
A report should include:
- Session ID and UTC timestamp.
- Vehicle identity and whether manually entered or read.
- Adapter identity and firmware if available.
- ECU scan results and statuses.
- DTC records and source/description confidence.
- Operations not attempted or unsupported.
- Simulated/real status.
- Software and definition database versions.

## 10. Contract change procedure
1. Open a PR changing this document.
2. Explain why and list producer/consumer impact.
3. Propose DTO/interface changes and migration.
4. Get approval from A, B and C (or project owner).
5. Update tests and all affected implementations in coordinated PRs.

## 11. Phase 1 scope
- Developer A delivers tasks A-101 to A-105.
- CAN, ISO-TP, UDS and freeze frame are deferred to later tasks.
- Until freeze frame is implemented, `ReadFreezeFrameAsync` returns `Unsupported`.

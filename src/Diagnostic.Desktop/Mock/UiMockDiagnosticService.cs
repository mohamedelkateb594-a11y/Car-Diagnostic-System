using Adapters.Abstractions;
using Contracts;

namespace Diagnostic.Desktop.Mock;

/// <summary>
/// TEMPORARY simulated diagnostic service for UI development.
/// All data is test data, flagged IsSimulated = true and SourceId = "ui-mock".
/// The UI never hard-codes this data: it only renders what IDiagnosticService returns.
/// </summary>
public sealed class UiMockDiagnosticService : IDiagnosticService
{
    private static readonly VehicleIdentity SimulatedVehicle = new(
        "TEST0000000000001", "Simulated Make", "Test Vehicle", 2020, "Simulated engine", "Test market");

    private static readonly EcuScanResult[] SimulatedScan =
    {
        new("ecm", "Engine Control Module", EcuScanStatus.Responded,
            new[]
            {
                new DtcRecord("P0300", "Random/Multiple Cylinder Misfire Detected", "Confirmed", "ecm", "ui-mock", true, false),
                new DtcRecord("P0420", "Catalyst System Efficiency Below Threshold (Bank 1)", "Pending", "ecm", "ui-mock", false, true),
            },
            "Simulated result"),
        new("tcm", "Transmission Control Module", EcuScanStatus.Responded,
            Array.Empty<DtcRecord>(), "Simulated result"),
        new("abs", "ABS / ESP", EcuScanStatus.NoResponse,
            Array.Empty<DtcRecord>(), "Simulated: no response"),
        new("srs", "Airbag (SRS)", EcuScanStatus.Unsupported,
            Array.Empty<DtcRecord>(), "Simulated: not supported"),
    };

    private static readonly (string Id, string Name, string Unit, double Base, double Amplitude)[] LiveParameters =
    {
        ("rpm", "Engine speed", "rpm", 850, 120),
        ("coolant_temp", "Coolant temperature", "degC", 88, 4),
        ("battery_voltage", "Battery voltage", "V", 13.9, 0.2),
    };

    private readonly IVciAdapter _adapter;
    private readonly HashSet<Guid> _sessions = new();
    private int _tick;

    public UiMockDiagnosticService(IVciAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    public Task<OperationResult<DiagnosticSessionInfo>> StartSessionAsync(
        string adapterId, VehicleIdentity? manualVehicle, CancellationToken ct)
    {
        if (!_adapter.IsConnected)
        {
            return Task.FromResult(MockResult.Fail<DiagnosticSessionInfo>(
                OperationStatus.NotConnected, "Adapter is not connected.", "NOT_CONNECTED"));
        }

        if (adapterId != UiMockVciAdapter.AdapterId)
        {
            return Task.FromResult(MockResult.Fail<DiagnosticSessionInfo>(
                OperationStatus.NotApplicable, "Unknown adapter id.", "UNKNOWN_ADAPTER"));
        }

        var info = new DiagnosticSessionInfo(
            Guid.NewGuid(), manualVehicle, UiMockVciAdapter.Info, DateTimeOffset.UtcNow, true);
        _sessions.Add(info.SessionId);
        return Task.FromResult(MockResult.Ok(info));
    }

    public Task<OperationResult<VehicleIdentity>> ReadVehicleIdentityAsync(Guid sessionId, CancellationToken ct)
        => Task.FromResult(WithSession(sessionId, () => MockResult.Ok(SimulatedVehicle)));

    public Task<OperationResult<IReadOnlyList<EcuScanResult>>> FullScanAsync(Guid sessionId, CancellationToken ct)
        => Task.FromResult(WithSession(sessionId, () =>
        {
            var allResponded = SimulatedScan.All(ecu => ecu.Status == EcuScanStatus.Responded);
            return MockResult.Ok<IReadOnlyList<EcuScanResult>>(
                SimulatedScan,
                allResponded ? OperationStatus.Success : OperationStatus.PartialSuccess);
        }));

    public Task<OperationResult<IReadOnlyList<DtcRecord>>> ReadDtcsAsync(
        Guid sessionId, string ecuId, CancellationToken ct)
        => Task.FromResult(WithSession(sessionId, () =>
        {
            var ecu = SimulatedScan.FirstOrDefault(candidate => candidate.EcuId == ecuId);
            if (ecu is null || ecu.Status != EcuScanStatus.Responded)
            {
                return MockResult.Fail<IReadOnlyList<DtcRecord>>(
                    OperationStatus.NotApplicable, "ECU did not respond in the simulated scan.", "ECU_NOT_AVAILABLE");
            }

            return MockResult.Ok<IReadOnlyList<DtcRecord>>(ecu.Dtcs);
        }));

    public Task<OperationResult<IReadOnlyList<LiveDataPoint>>> ReadLiveDataAsync(
        Guid sessionId, string ecuId, IReadOnlyList<string> parameterIds, CancellationToken ct)
        => Task.FromResult(WithSession(sessionId, () =>
        {
            // An empty request means "the mock's default parameter set"; a real service decides its own rule.
            var wanted = parameterIds.Count == 0
                ? LiveParameters
                : LiveParameters.Where(p => parameterIds.Contains(p.Id, StringComparer.OrdinalIgnoreCase)).ToArray();

            if (wanted.Length == 0)
            {
                return MockResult.Fail<IReadOnlyList<LiveDataPoint>>(
                    OperationStatus.Unsupported, "None of the requested parameters is supported.", "UNKNOWN_PARAMETER");
            }

            var tick = Interlocked.Increment(ref _tick);
            var now = DateTimeOffset.UtcNow;
            var points = wanted
                .Select(p => new LiveDataPoint(
                    p.Id, p.Name, p.Base + (p.Amplitude * Math.Sin(tick / 4.0)), p.Unit, now, true))
                .ToList();
            return MockResult.Ok<IReadOnlyList<LiveDataPoint>>(points);
        }));

    public Task<OperationResult<FreezeFrameRecord?>> ReadFreezeFrameAsync(
        Guid sessionId, string ecuId, string? dtcCode, CancellationToken ct)
        => Task.FromResult(WithSession(sessionId, () =>
            MockResult.Fail<FreezeFrameRecord?>(
                OperationStatus.Unsupported, "Freeze frame is not simulated.", "MOCK_NO_FREEZE_FRAME")));

    private OperationResult<T> WithSession<T>(Guid sessionId, Func<OperationResult<T>> action)
        => _sessions.Contains(sessionId) && _adapter.IsConnected
            ? action()
            : MockResult.Fail<T>(OperationStatus.NotConnected, "No active session.", "NO_SESSION");
}

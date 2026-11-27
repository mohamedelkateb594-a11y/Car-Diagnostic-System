using System.Diagnostics.CodeAnalysis;
using Contracts;
using Diagnostic.Protocols;

namespace Diagnostic.Core;

/// <summary>
/// Read-only OBD-II implementation of <see cref="IDiagnosticService"/>.
/// Temporary decision: one generic ECU (<see cref="Obd2EcuId"/>) and one session per connection,
/// until ECU addressing is defined by a contract and verified vehicle definitions.
/// </summary>
public sealed class DiagnosticService : IDiagnosticService
{
    public const string Obd2EcuId = "obd2";
    private const byte LastSupportPid = 0x40;

    // Temporary decision: only the first freeze frame is read.
    private const byte FreezeFrameNumber = 0;

    private readonly VciConnectionManager _connection;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _sessionGate = new(1, 1);
    private Session? _session;

    public DiagnosticService(VciConnectionManager connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;
        _time = connection.Time;
    }

    public async Task<OperationResult<DiagnosticSessionInfo>> StartSessionAsync(
        string adapterId, VehicleIdentity? manualVehicle, CancellationToken ct)
    {
        try
        {
            await _sessionGate.WaitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return Results.Cancelled<DiagnosticSessionInfo>(_time, false);
        }

        try
        {
            if (_session is not null && IsAlive(_session))
            {
                return Results.Fail<DiagnosticSessionInfo>(
                    OperationStatus.Failed,
                    "SessionActive",
                    "End the active session first.",
                    _time,
                    _session.Info.IsSimulated);
            }

            var connect = await _connection.ConnectAsync(adapterId, ct);
            if (connect.Status != OperationStatus.Success || connect.Data is null)
            {
                return new OperationResult<DiagnosticSessionInfo>(
                    connect.Status, null, connect.Message, connect.ErrorCode, connect.TimestampUtc, connect.IsSimulated);
            }

            var info = new DiagnosticSessionInfo(
                Guid.NewGuid(), manualVehicle, connect.Data, _time.GetUtcNow(), connect.IsSimulated);
            _session = new Session(info, _connection.Generation);
            return Result(_session, OperationStatus.Success, info, null, null);
        }
        finally
        {
            _sessionGate.Release();
        }
    }

    /// <summary>Ends the session and disconnects. Not part of the shared contract.</summary>
    public async Task<OperationResult<bool>> EndSessionAsync(Guid sessionId, CancellationToken ct)
    {
        try
        {
            await _sessionGate.WaitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return Results.Cancelled<bool>(_time, false);
        }

        try
        {
            var session = _session;
            if (session is null || session.Info.SessionId != sessionId)
            {
                return UnknownSession<bool>();
            }

            if (IsAlive(session))
            {
                var disconnect = await _connection.DisconnectAsync(ct);
                if (disconnect.Status != OperationStatus.Success)
                {
                    return disconnect;
                }
            }

            _session = null;
            return Result(session, OperationStatus.Success, true, null, null);
        }
        finally
        {
            _sessionGate.Release();
        }
    }

    public async Task<OperationResult<VehicleIdentity>> ReadVehicleIdentityAsync(Guid sessionId, CancellationToken ct)
    {
        if (!TryGetSession<VehicleIdentity>(sessionId, ct, out var session, out var failure))
        {
            return failure;
        }

        var vin = await ExchangeAsync(session, Obd2Request.Vin(), Obd2Parser.ParseVin, ct);

        // Only values read from the ECU are returned; manual vehicle data stays in the session info.
        var identity = vin.Status == OperationStatus.Success && vin.Data is not null
            ? new VehicleIdentity(vin.Data, null, null, null, null, null)
            : null;
        return Result(session, vin.Status, identity, vin.Message, vin.ErrorCode);
    }

    public async Task<OperationResult<IReadOnlyList<EcuScanResult>>> FullScanAsync(Guid sessionId, CancellationToken ct)
    {
        if (!TryGetSession<IReadOnlyList<EcuScanResult>>(sessionId, ct, out var session, out var failure))
        {
            return failure;
        }

        var dtcs = await ReadObd2DtcsAsync(session, ct);
        if (IsAbort(dtcs.Status))
        {
            return Result<IReadOnlyList<EcuScanResult>>(session, dtcs.Status, null, dtcs.Message, dtcs.ErrorCode);
        }

        var ecuStatus = dtcs.Status switch
        {
            OperationStatus.Success or OperationStatus.PartialSuccess => EcuScanStatus.Responded,
            OperationStatus.Timeout => EcuScanStatus.NoResponse,
            OperationStatus.Unsupported => EcuScanStatus.Unsupported,
            _ => EcuScanStatus.Error,
        };
        var ecu = new EcuScanResult(
            Obd2EcuId,
            "Generic OBD-II ECU",
            ecuStatus,
            dtcs.Data ?? [],
            dtcs.Status == OperationStatus.Success ? null : $"{dtcs.Status}: {dtcs.Message}");

        return Result<IReadOnlyList<EcuScanResult>>(session, dtcs.Status, [ecu], dtcs.Message, dtcs.ErrorCode);
    }

    public async Task<OperationResult<IReadOnlyList<DtcRecord>>> ReadDtcsAsync(
        Guid sessionId, string ecuId, CancellationToken ct)
    {
        if (!TryGetSession<IReadOnlyList<DtcRecord>>(sessionId, ct, out var session, out var failure))
        {
            return failure;
        }

        return ecuId == Obd2EcuId
            ? await ReadObd2DtcsAsync(session, ct)
            : UnknownEcu<IReadOnlyList<DtcRecord>>(session);
    }

    /// <summary>Lists decodable parameters the ECU reports as supported. Not part of the shared contract.</summary>
    public async Task<OperationResult<IReadOnlyList<SupportedParameter>>> GetSupportedParametersAsync(
        Guid sessionId, string ecuId, CancellationToken ct)
    {
        if (!TryGetSession<IReadOnlyList<SupportedParameter>>(sessionId, ct, out var session, out var failure))
        {
            return failure;
        }

        if (ecuId != Obd2EcuId)
        {
            return UnknownEcu<IReadOnlyList<SupportedParameter>>(session);
        }

        var support = await GetSupportedPidsAsync(session, ct);
        IReadOnlyList<SupportedParameter>? parameters = support.Data is null
            ? null
            : Obd2PidCatalog.All
                .Where(d => support.Data.Contains(d.Pid))
                .Select(d => new SupportedParameter(d.ParameterId, d.Name, d.Unit))
                .ToArray();
        return Result(session, support.Status, parameters, support.Message, support.ErrorCode);
    }

    public async Task<OperationResult<IReadOnlyList<LiveDataPoint>>> ReadLiveDataAsync(
        Guid sessionId, string ecuId, IReadOnlyList<string> parameterIds, CancellationToken ct)
    {
        if (!TryGetSession<IReadOnlyList<LiveDataPoint>>(sessionId, ct, out var session, out var failure))
        {
            return failure;
        }

        if (ecuId != Obd2EcuId)
        {
            return UnknownEcu<IReadOnlyList<LiveDataPoint>>(session);
        }

        if (parameterIds is null || parameterIds.Count == 0)
        {
            return Result<IReadOnlyList<LiveDataPoint>>(
                session, OperationStatus.Failed, null, "No parameters were requested.", "InvalidArgument");
        }

        var support = await GetSupportedPidsAsync(session, ct);
        if (support.Data is null)
        {
            // Unknown support is never treated as support.
            return Result<IReadOnlyList<LiveDataPoint>>(
                session, support.Status, null, support.Message, support.ErrorCode);
        }

        var points = new List<LiveDataPoint>();
        var omitted = new List<string>();
        OperationResult<double>? firstFailure = null;
        foreach (var parameterId in parameterIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Obd2PidCatalog.TryGet(parameterId, out var definition))
            {
                omitted.Add($"{parameterId}: unknown parameter");
                continue;
            }

            if (!support.Data.Contains(definition.Pid))
            {
                omitted.Add($"{parameterId}: not supported by the ECU");
                continue;
            }

            var value = await ExchangeAsync(
                session,
                Obd2Request.CurrentData(definition.Pid),
                response => Obd2Parser.ParseLiveValue(response, definition),
                ct);
            if (IsAbort(value.Status))
            {
                return Result<IReadOnlyList<LiveDataPoint>>(
                    session, value.Status, null, value.Message, value.ErrorCode);
            }

            if (value.Status != OperationStatus.Success)
            {
                firstFailure ??= value;
                omitted.Add($"{parameterId}: {value.Status} {value.Message}");
                continue;
            }

            points.Add(new LiveDataPoint(
                definition.ParameterId,
                definition.Name,
                value.Data,
                definition.Unit,
                _time.GetUtcNow(),
                value.IsSimulated));
        }

        if (omitted.Count == 0)
        {
            return Result<IReadOnlyList<LiveDataPoint>>(session, OperationStatus.Success, points, null, null);
        }

        var status = points.Count > 0
            ? OperationStatus.PartialSuccess
            : firstFailure?.Status ?? OperationStatus.Unsupported;
        return Result<IReadOnlyList<LiveDataPoint>>(
            session,
            status,
            points,
            string.Join("; ", omitted),
            firstFailure?.ErrorCode ?? "ParameterUnavailable");
    }

    /// <summary>
    /// Success with null Data means that no freeze frame is stored (for the requested code).
    /// CapturedAtUtc is null because the ECU does not report a capture time.
    /// </summary>
    public async Task<OperationResult<FreezeFrameRecord?>> ReadFreezeFrameAsync(
        Guid sessionId, string ecuId, string? dtcCode, CancellationToken ct)
    {
        if (!TryGetSession<FreezeFrameRecord?>(sessionId, ct, out var session, out var failure))
        {
            return failure;
        }

        if (ecuId != Obd2EcuId)
        {
            return UnknownEcu<FreezeFrameRecord?>(session);
        }

        var trigger = await ExchangeAsync(
            session,
            Obd2FreezeFrame.Request(Obd2FreezeFrame.TriggerDtcPid, FreezeFrameNumber),
            response => Obd2FreezeFrame.ParseTriggerDtc(response, FreezeFrameNumber),
            ct);
        if (trigger.Status != OperationStatus.Success || trigger.Data is null)
        {
            return Result<FreezeFrameRecord?>(session, trigger.Status, null, trigger.Message, trigger.ErrorCode);
        }

        if (trigger.Data.Length == 0)
        {
            return Result<FreezeFrameRecord?>(
                session, OperationStatus.Success, null, "No freeze frame is stored.", null);
        }

        if (dtcCode is not null && !string.Equals(dtcCode, trigger.Data, StringComparison.OrdinalIgnoreCase))
        {
            return Result<FreezeFrameRecord?>(
                session,
                OperationStatus.Success,
                null,
                $"No freeze frame for {dtcCode}; the stored frame belongs to {trigger.Data}.",
                null);
        }

        // Temporary decision: service 02 uses the same PIDs as service 01, so the service 01
        // support list selects what to request. A rejected PID is omitted and reported.
        var support = await GetSupportedPidsAsync(session, ct);
        if (support.Data is null)
        {
            return Result<FreezeFrameRecord?>(session, support.Status, null, support.Message, support.ErrorCode);
        }

        var values = new List<LiveDataPoint>();
        var omitted = new List<string>();
        string? firstErrorCode = null;
        foreach (var definition in Obd2PidCatalog.All.Where(d => support.Data.Contains(d.Pid)))
        {
            var value = await ExchangeAsync(
                session,
                Obd2FreezeFrame.Request(definition.Pid, FreezeFrameNumber),
                response => Obd2FreezeFrame.ParseValue(response, FreezeFrameNumber, definition),
                ct);
            if (IsAbort(value.Status))
            {
                return Result<FreezeFrameRecord?>(session, value.Status, null, value.Message, value.ErrorCode);
            }

            if (value.Status != OperationStatus.Success)
            {
                firstErrorCode ??= value.ErrorCode;
                omitted.Add($"{definition.ParameterId}: {value.Status} {value.Message}");
                continue;
            }

            values.Add(new LiveDataPoint(
                definition.ParameterId,
                definition.Name,
                value.Data,
                definition.Unit,
                _time.GetUtcNow(),
                value.IsSimulated));
        }

        var snapshot = new FreezeFrameRecord(trigger.Data, values, null);
        return omitted.Count == 0
            ? Result<FreezeFrameRecord?>(session, OperationStatus.Success, snapshot, null, null)
            : Result<FreezeFrameRecord?>(
                session, OperationStatus.PartialSuccess, snapshot, string.Join("; ", omitted), firstErrorCode);
    }

    private static bool IsAbort(OperationStatus status) =>
        status is OperationStatus.Cancelled or OperationStatus.NotConnected;

    private static OperationResult<T> Result<T>(
        Session session, OperationStatus status, T? data, string? message, string? errorCode, TimeProvider time) =>
        new(status, data, message, errorCode, time.GetUtcNow(), session.Info.IsSimulated);

    private OperationResult<T> Result<T>(
        Session session, OperationStatus status, T? data, string? message, string? errorCode) =>
        Result(session, status, data, message, errorCode, _time);

    private OperationResult<T> UnknownSession<T>() =>
        Results.Fail<T>(OperationStatus.Failed, "UnknownSession", "The session does not exist.", _time, false);

    private OperationResult<T> UnknownEcu<T>(Session session) =>
        Results.Fail<T>(
            OperationStatus.Unsupported,
            "UnknownEcu",
            $"Only the ECU id '{Obd2EcuId}' is supported.",
            _time,
            session.Info.IsSimulated);

    private bool IsAlive(Session session) =>
        _connection.IsConnected && _connection.Generation == session.Generation;

    private bool TryGetSession<T>(
        Guid sessionId,
        CancellationToken ct,
        [NotNullWhen(true)] out Session? session,
        [NotNullWhen(false)] out OperationResult<T>? failure)
    {
        var current = _session;
        session = null;
        if (current is null || current.Info.SessionId != sessionId)
        {
            failure = UnknownSession<T>();
            return false;
        }

        if (ct.IsCancellationRequested)
        {
            failure = Results.Cancelled<T>(_time, current.Info.IsSimulated);
            return false;
        }

        if (!IsAlive(current))
        {
            failure = Results.Fail<T>(
                OperationStatus.NotConnected,
                "NotConnected",
                "The session connection is closed.",
                _time,
                current.Info.IsSimulated);
            return false;
        }

        session = current;
        failure = null;
        return true;
    }

    private async Task<OperationResult<T>> ExchangeAsync<T>(
        Session session, byte[] request, Func<byte[], ParseResult<T>> parse, CancellationToken ct)
    {
        var sent = await _connection.SendAsync(request, session.Generation, ct);
        if (sent.Status != OperationStatus.Success)
        {
            return Result<T>(session, sent.Status, default, sent.Message, sent.ErrorCode);
        }

        if (sent.Data is null)
        {
            return Result<T>(
                session, OperationStatus.InvalidResponse, default, "The adapter returned no data.", "InvalidResponse");
        }

        var parsed = parse(sent.Data);
        return Result(session, parsed.Status, parsed.Value, parsed.Message, parsed.ErrorCode);
    }

    private async Task<OperationResult<IReadOnlyList<DtcRecord>>> ReadObd2DtcsAsync(
        Session session, CancellationToken ct)
    {
        var stored = await ExchangeAsync(
            session,
            Obd2Request.StoredDtcs(),
            response => Obd2Parser.ParseDtcs(response, Obd2Service.StoredDtcs),
            ct);
        if (IsAbort(stored.Status))
        {
            return Result<IReadOnlyList<DtcRecord>>(session, stored.Status, null, stored.Message, stored.ErrorCode);
        }

        var pending = await ExchangeAsync(
            session,
            Obd2Request.PendingDtcs(),
            response => Obd2Parser.ParseDtcs(response, Obd2Service.PendingDtcs),
            ct);
        if (IsAbort(pending.Status))
        {
            return Result<IReadOnlyList<DtcRecord>>(session, pending.Status, null, pending.Message, pending.ErrorCode);
        }

        var storedOk = stored.Status == OperationStatus.Success;
        var pendingOk = pending.Status == OperationStatus.Success;
        if (!storedOk && !pendingOk)
        {
            return Result<IReadOnlyList<DtcRecord>>(session, stored.Status, null, stored.Message, stored.ErrorCode);
        }

        var records = Merge(stored.Data ?? [], pending.Data ?? []);
        if (storedOk && pendingOk)
        {
            return Result(session, OperationStatus.Success, records, null, null);
        }

        var failed = storedOk ? pending : stored;
        var label = storedOk ? "Pending" : "Stored";
        return Result(
            session,
            OperationStatus.PartialSuccess,
            records,
            $"{label} DTC read failed: {failed.Status}. {failed.Message}",
            failed.ErrorCode);
    }

    private static IReadOnlyList<DtcRecord> Merge(IReadOnlyList<string> stored, IReadOnlyList<string> pending) =>
        stored
            .Union(pending)
            .Select(code =>
            {
                var isStored = stored.Contains(code);
                var isPending = pending.Contains(code);
                var status = isStored && isPending ? "stored,pending" : isStored ? "stored" : "pending";
                // No description: no licensed DTC text source has been selected.
                return new DtcRecord(code, null, status, Obd2EcuId, null, isStored, isPending);
            })
            .ToArray();

    private async Task<OperationResult<IReadOnlySet<byte>>> GetSupportedPidsAsync(Session session, CancellationToken ct)
    {
        if (session.SupportedPids is { } cached)
        {
            return Result(session, OperationStatus.Success, cached, null, null);
        }

        var supported = new HashSet<byte>();
        byte basePid = 0x00;
        while (true)
        {
            var page = await ExchangeAsync(
                session,
                Obd2Request.CurrentData(basePid),
                response => Obd2Parser.ParseSupportedPids(response, basePid),
                ct);
            if (page.Status != OperationStatus.Success || page.Data is null)
            {
                return Result<IReadOnlySet<byte>>(session, page.Status, null, page.Message, page.ErrorCode);
            }

            supported.UnionWith(page.Data);
            var next = (byte)(basePid + 0x20);
            if (next > LastSupportPid || !page.Data.Contains(next))
            {
                break;
            }

            basePid = next;
        }

        session.SupportedPids = supported;
        return Result<IReadOnlySet<byte>>(session, OperationStatus.Success, supported, null, null);
    }

    private sealed class Session(DiagnosticSessionInfo info, long generation)
    {
        public DiagnosticSessionInfo Info { get; } = info;

        public long Generation { get; } = generation;

        public IReadOnlySet<byte>? SupportedPids { get; set; }
    }
}

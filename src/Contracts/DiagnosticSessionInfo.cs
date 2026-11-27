namespace Contracts;

public sealed record DiagnosticSessionInfo(
    Guid SessionId,
    VehicleIdentity? Vehicle,
    AdapterInfo Adapter,
    DateTimeOffset StartedAtUtc,
    bool IsSimulated);

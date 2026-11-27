namespace Contracts;

public sealed record LiveDataPoint(
    string ParameterId,
    string Name,
    double Value,
    string Unit,
    DateTimeOffset TimestampUtc,
    bool IsSimulated);

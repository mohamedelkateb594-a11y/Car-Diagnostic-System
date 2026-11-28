namespace Contracts;

public sealed record FreezeFrameRecord(
    string? DtcCode,
    IReadOnlyList<LiveDataPoint> Values,
    DateTimeOffset? CapturedAtUtc);

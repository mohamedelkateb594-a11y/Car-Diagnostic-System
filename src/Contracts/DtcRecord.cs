namespace Contracts;

public sealed record DtcRecord(
    string Code,
    string? Description,
    string? Status,
    string? EcuId,
    string? SourceId,
    bool IsConfirmed,
    bool IsPending);

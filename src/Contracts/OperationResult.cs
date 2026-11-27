namespace Contracts;

public sealed record OperationResult<T>(
    OperationStatus Status,
    T? Data,
    string? Message,
    string? ErrorCode,
    DateTimeOffset TimestampUtc,
    bool IsSimulated);

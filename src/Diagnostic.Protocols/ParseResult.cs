using Contracts;

namespace Diagnostic.Protocols;

/// <summary>Outcome of a protocol step. Check <see cref="Status"/> before reading <see cref="Value"/>.</summary>
public sealed record ParseResult<T>(OperationStatus Status, T? Value, string? Message, string? ErrorCode)
{
    public static ParseResult<T> Ok(T value) => new(OperationStatus.Success, value, null, null);

    public static ParseResult<T> Fail(OperationStatus status, string errorCode, string message) =>
        new(status, default, message, errorCode);
}

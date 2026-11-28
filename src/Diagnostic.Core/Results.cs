using Contracts;

namespace Diagnostic.Core;

internal static class Results
{
    public static OperationResult<T> Fail<T>(
        OperationStatus status, string errorCode, string message, TimeProvider time, bool simulated) =>
        new(status, default, message, errorCode, time.GetUtcNow(), simulated);

    public static OperationResult<T> Cancelled<T>(TimeProvider time, bool simulated) =>
        Fail<T>(OperationStatus.Cancelled, "Cancelled", "The operation was cancelled.", time, simulated);
}

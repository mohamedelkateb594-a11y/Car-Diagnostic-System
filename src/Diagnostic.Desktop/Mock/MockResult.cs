using Contracts;

namespace Diagnostic.Desktop.Mock;

/// <summary>
/// TEMPORARY UI-side stand-in. Every result carries IsSimulated = true.
/// Replaced by the real Diagnostic.Core / Adapters.Mock implementations in App.xaml.cs
/// (the only place that knows about it) once Developer A's services are available.
/// </summary>
internal static class MockResult
{
    public static OperationResult<T> Ok<T>(T data, OperationStatus status = OperationStatus.Success, string? message = null)
        => new(status, data, message, null, DateTimeOffset.UtcNow, true);

    public static OperationResult<T> Fail<T>(OperationStatus status, string message, string errorCode)
        => new(status, default, message, errorCode, DateTimeOffset.UtcNow, true);
}

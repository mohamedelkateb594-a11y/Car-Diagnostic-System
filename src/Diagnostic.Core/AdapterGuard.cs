using Contracts;

namespace Diagnostic.Core;

/// <summary>Layer boundary: turns adapter hangs and exceptions into structured results.</summary>
internal static class AdapterGuard
{
    public static async Task<OperationResult<T>> ExecuteAsync<T>(
        Func<CancellationToken, Task<OperationResult<T>>> operation,
        TimeSpan timeout,
        TimeProvider time,
        bool simulated,
        CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            return Results.Cancelled<T>(time, simulated);
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        try
        {
            return await operation(linked.Token).WaitAsync(timeout, time, ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            await linked.CancelAsync().ConfigureAwait(false);
            return Results.Fail<T>(
                OperationStatus.Timeout, "Timeout", "The adapter did not answer in time.", time, simulated);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await linked.CancelAsync().ConfigureAwait(false);
            return Results.Cancelled<T>(time, simulated);
        }
        catch (Exception ex)
        {
            return Results.Fail<T>(
                OperationStatus.TransportError,
                "AdapterException",
                $"{ex.GetType().Name}: {ex.Message}",
                time,
                simulated);
        }
    }
}

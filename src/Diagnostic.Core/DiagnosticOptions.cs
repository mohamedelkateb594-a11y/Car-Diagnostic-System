namespace Diagnostic.Core;

public sealed record DiagnosticOptions
{
    /// <summary>Timeout passed to the adapter for one request.</summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(2);

    public TimeSpan ConnectionTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Extra time before the Core gives up on an adapter that ignores its own timeout.</summary>
    public TimeSpan AdapterGracePeriod { get; init; } = TimeSpan.FromMilliseconds(250);
}

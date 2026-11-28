using Adapters.Abstractions;
using Adapters.Mock;
using Contracts;

namespace Diagnostic.Desktop.Composition;

/// <summary>
/// Developer A's real Core running over the SIMULATED adapter (src/Adapters.Mock).
/// Per src/Diagnostic.Core/README.md the IVciAdapter given to the UI must be ManagedAdapter,
/// which shares one connection with the service.
/// Nothing here talks to hardware. A real VCI backend replaces this class (and only this class).
/// </summary>
public sealed class SimulatedBackend : IDiagnosticBackend
{
    private readonly MockDiagnosticService _service = new();

    public string DisplayName => "Simulated engine (Adapters.Mock)";

    public IVciAdapter Adapter => _service.ManagedAdapter;

    public IDiagnosticService Diagnostics => _service;

    public ValueTask DisposeAsync() => _service.DisposeAsync();
}

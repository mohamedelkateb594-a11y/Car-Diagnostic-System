using Adapters.Abstractions;
using Contracts;

namespace Diagnostic.Desktop.Composition;

/// <summary>
/// The one swap point between the UI and the engine. Screens only see IVciAdapter and
/// IDiagnosticService (shared contracts); which engine stands behind them is decided by the
/// IDiagnosticBackend registered in <see cref="ServiceRegistration"/>.
/// </summary>
public interface IDiagnosticBackend : IAsyncDisposable
{
    /// <summary>Shown in the window so it is always clear which engine is running.</summary>
    string DisplayName { get; }

    IVciAdapter Adapter { get; }

    IDiagnosticService Diagnostics { get; }
}

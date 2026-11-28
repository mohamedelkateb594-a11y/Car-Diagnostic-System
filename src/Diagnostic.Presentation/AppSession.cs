using Contracts;
using Diagnostic.Presentation.Mvvm;

namespace Diagnostic.Presentation;

/// <summary>
/// Shared state of the current diagnostic session, observed by all screens.
/// Holds only what services returned; nothing here is invented by the UI.
/// </summary>
public sealed class AppSession : ObservableObject
{
    private DiagnosticSessionInfo? _info;
    private VehicleIdentity? _vehicle;
    private IReadOnlyList<EcuScanResult> _lastScan = Array.Empty<EcuScanResult>();

    public DiagnosticSessionInfo? Info => _info;

    public VehicleIdentity? Vehicle => _vehicle;

    public IReadOnlyList<EcuScanResult> LastScan => _lastScan;

    public bool IsActive => _info is not null;

    public bool IsSimulated => _info?.IsSimulated ?? false;

    public void SetSession(DiagnosticSessionInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        _info = info;
        _vehicle = info.Vehicle;
        _lastScan = Array.Empty<EcuScanResult>();
        NotifyAll();
    }

    public void SetVehicle(VehicleIdentity vehicle)
    {
        ArgumentNullException.ThrowIfNull(vehicle);
        _vehicle = vehicle;
        OnPropertyChanged(nameof(Vehicle));
    }

    public void SetScan(IReadOnlyList<EcuScanResult> scan)
    {
        ArgumentNullException.ThrowIfNull(scan);
        _lastScan = scan;
        OnPropertyChanged(nameof(LastScan));
    }

    public void Clear()
    {
        _info = null;
        _vehicle = null;
        _lastScan = Array.Empty<EcuScanResult>();
        NotifyAll();
    }

    private void NotifyAll()
    {
        OnPropertyChanged(nameof(Info));
        OnPropertyChanged(nameof(Vehicle));
        OnPropertyChanged(nameof(LastScan));
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(IsSimulated));
    }
}

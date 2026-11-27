using System.Collections.ObjectModel;
using System.ComponentModel;
using Contracts;
using Diagnostic.Desktop.Services;

namespace Diagnostic.Desktop.ViewModels;

/// <summary>Base for screens that act on one ECU chosen from the last full scan.</summary>
public abstract class EcuScopedViewModel : OperationScreenViewModel
{
    private EcuScanResult? _selectedEcu;
    private bool _hasScan;

    protected EcuScopedViewModel(AppSession session)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Session.PropertyChanged += OnSessionChanged;
        RefreshEcus();
    }

    public AppSession Session { get; }

    /// <summary>Only ECUs that responded in the last scan are offered.</summary>
    public ObservableCollection<EcuScanResult> AvailableEcus { get; } = new();

    public EcuScanResult? SelectedEcu
    {
        get => _selectedEcu;
        set => SetProperty(ref _selectedEcu, value);
    }

    public bool HasScan
    {
        get => _hasScan;
        private set => SetProperty(ref _hasScan, value);
    }

    protected virtual void OnEcusRefreshed()
    {
    }

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppSession.LastScan) or nameof(AppSession.Info))
        {
            RefreshEcus();
            OnEcusRefreshed();
        }
    }

    private void RefreshEcus()
    {
        var previousId = SelectedEcu?.EcuId;
        AvailableEcus.Clear();
        foreach (var ecu in Session.LastScan.Where(candidate => candidate.Status == EcuScanStatus.Responded))
        {
            AvailableEcus.Add(ecu);
        }

        HasScan = Session.LastScan.Count > 0;
        SelectedEcu = AvailableEcus.FirstOrDefault(candidate => candidate.EcuId == previousId)
                      ?? AvailableEcus.FirstOrDefault();
    }
}

using System.Collections.ObjectModel;
using Contracts;
using Diagnostic.Desktop.Mvvm;
using Diagnostic.Desktop.Services;

namespace Diagnostic.Desktop.ViewModels;

/// <summary>Reads and shows DTCs. Read-only: clearing DTCs is not in the approved contracts (API_CONTRACTS.md section 8).</summary>
public sealed class DtcViewModel : EcuScopedViewModel
{
    private readonly IDiagnosticService _diagnostics;

    public DtcViewModel(IDiagnosticService diagnostics, AppSession session)
        : base(session)
    {
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));

        ReadCommand = new AsyncRelayCommand(
            ReadAsync, () => !IsBusy && Session.IsActive && SelectedEcu is not null);
    }

    public ObservableCollection<DtcRecord> Dtcs { get; } = new();

    public AsyncRelayCommand ReadCommand { get; }

    protected override void OnEcusRefreshed() => Dtcs.Clear();

    private Task ReadAsync() => RunBusyAsync(async () =>
    {
        var info = Session.Info;
        var ecu = SelectedEcu;
        if (info is null || ecu is null)
        {
            return;
        }

        var result = await _diagnostics.ReadDtcsAsync(info.SessionId, ecu.EcuId, CancellationToken.None);
        Dtcs.Clear();
        if (IsUsable(result))
        {
            foreach (var dtc in result.Data!)
            {
                Dtcs.Add(dtc);
            }

            SetStatus(Dtcs.Count == 0 ? "DtcNone" : "OpStatus_Success");
            return;
        }

        SetResultStatus(result);
    });
}

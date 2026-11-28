using System.Collections.ObjectModel;
using Contracts;
using Diagnostic.Presentation.Mvvm;
using Microsoft.Extensions.Logging;

namespace Diagnostic.Presentation.ViewModels;

/// <summary>Reads and shows DTCs. Read-only: clearing DTCs is not in the approved contracts.</summary>
public sealed class DtcViewModel : EcuScopedViewModel
{
    private readonly IDiagnosticService _diagnostics;

    public DtcViewModel(
        IDiagnosticService diagnostics,
        AppSession session,
        ILogger<DtcViewModel>? logger = null)
        : base(session, logger)
    {
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));

        ReadCommand = Track(new AsyncRelayCommand(
            ReadAsync, () => !IsBusy && Session.IsActive && SelectedEcu is not null));
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
        }

        // "No codes" is claimed only for a complete Success. An empty list with PartialSuccess
        // does not mean the ECU has no codes (src/Diagnostic.Core/README.md), so the engine's
        // status and message are shown instead.
        if (result.Status == OperationStatus.Success && Dtcs.Count == 0)
        {
            SetStatus("DtcNone");
        }
        else
        {
            SetResultStatus(result);
        }
    });
}

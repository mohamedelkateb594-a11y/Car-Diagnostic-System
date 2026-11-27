using System.Collections.ObjectModel;
using Contracts;
using Diagnostic.Desktop.Mvvm;
using Diagnostic.Desktop.Services;

namespace Diagnostic.Desktop.ViewModels;

/// <param name="StatusKey">Localization key, e.g. EcuStatus_Responded.</param>
public sealed record EcuRow(string EcuId, string DisplayName, string StatusKey, int DtcCount, string? Notes);

public sealed class ScanViewModel : OperationScreenViewModel
{
    private readonly IDiagnosticService _diagnostics;

    public ScanViewModel(IDiagnosticService diagnostics, AppSession session)
    {
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        Session = session ?? throw new ArgumentNullException(nameof(session));

        RunCommand = new AsyncRelayCommand(RunAsync, () => !IsBusy && Session.IsActive);
    }

    public AppSession Session { get; }

    public ObservableCollection<EcuRow> Rows { get; } = new();

    public AsyncRelayCommand RunCommand { get; }

    private Task RunAsync() => RunBusyAsync(async () =>
    {
        var info = Session.Info;
        if (info is null)
        {
            return;
        }

        var result = await _diagnostics.FullScanAsync(info.SessionId, CancellationToken.None);
        if (IsUsable(result))
        {
            var scan = result.Data!;
            Rows.Clear();
            foreach (var ecu in scan)
            {
                Rows.Add(new EcuRow(ecu.EcuId, ecu.DisplayName, $"EcuStatus_{ecu.Status}", ecu.Dtcs.Count, ecu.Notes));
            }

            Session.SetScan(scan);
        }

        SetResultStatus(result);
    });
}

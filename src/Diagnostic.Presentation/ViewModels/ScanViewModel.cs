using System.Collections.ObjectModel;
using System.ComponentModel;
using Contracts;
using Diagnostic.Presentation.Mvvm;
using Microsoft.Extensions.Logging;

namespace Diagnostic.Presentation.ViewModels;

/// <param name="StatusKey">Localization key, e.g. EcuStatus_Responded.</param>
public sealed record EcuRow(string EcuId, string DisplayName, string StatusKey, int DtcCount, string? Notes);

public sealed class ScanViewModel : OperationScreenViewModel
{
    private readonly IDiagnosticService _diagnostics;

    public ScanViewModel(
        IDiagnosticService diagnostics,
        AppSession session,
        ILogger<ScanViewModel>? logger = null)
        : base(logger)
    {
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        Session = session ?? throw new ArgumentNullException(nameof(session));
        ObserveSession(Session);
        Session.PropertyChanged += OnSessionChanged;

        RunCommand = Track(new AsyncRelayCommand(RunAsync, () => !IsBusy && Session.IsActive));
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

        // The engine returns the ECU list even when the ECU failed (Timeout, TransportError, ...),
        // so rows are shown whenever Data is present; the status line carries the result.
        if (result.Data is not null)
        {
            Rows.Clear();
            foreach (var ecu in result.Data)
            {
                Rows.Add(new EcuRow(ecu.EcuId, ecu.DisplayName, $"EcuStatus_{ecu.Status}", ecu.Dtcs.Count, ecu.Notes));
            }

            Session.SetScan(result.Data);
        }

        SetResultStatus(result);
    });

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        // A new or ended session makes the old rows meaningless.
        if (e.PropertyName == nameof(AppSession.Info))
        {
            Rows.Clear();
        }
    }
}

using System.Collections.ObjectModel;
using Adapters.Abstractions;
using Contracts;
using Diagnostic.Presentation.Mvvm;
using Microsoft.Extensions.Logging;

namespace Diagnostic.Presentation.ViewModels;

public sealed class ConnectionViewModel : OperationScreenViewModel
{
    private readonly IVciAdapter _adapter;
    private readonly IDiagnosticService _diagnostics;
    private AdapterInfo? _selectedDevice;

    public ConnectionViewModel(
        IVciAdapter adapter,
        IDiagnosticService diagnostics,
        AppSession session,
        ILogger<ConnectionViewModel>? logger = null)
        : base(logger)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        Session = session ?? throw new ArgumentNullException(nameof(session));
        ObserveSession(Session);

        DiscoverCommand = Track(new AsyncRelayCommand(DiscoverAsync, () => !IsBusy));
        ConnectCommand = Track(new AsyncRelayCommand(
            ConnectAsync, () => !IsBusy && SelectedDevice is not null && !Session.IsActive));
        DisconnectCommand = Track(new AsyncRelayCommand(DisconnectAsync, () => !IsBusy && Session.IsActive));
    }

    public AppSession Session { get; }

    public ObservableCollection<AdapterInfo> Devices { get; } = new();

    public AdapterInfo? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (SetProperty(ref _selectedDevice, value))
            {
                RaiseCanExecuteChanged();
            }
        }
    }

    public AsyncRelayCommand DiscoverCommand { get; }

    public AsyncRelayCommand ConnectCommand { get; }

    public AsyncRelayCommand DisconnectCommand { get; }

    // Discovery only lists devices. Connecting always needs an explicit user action.
    // A failed discovery throws (documented by the engine); RunBusyAsync turns it into a status.
    private Task DiscoverAsync() => RunBusyAsync(async () =>
    {
        var found = await _adapter.DiscoverAsync(CancellationToken.None);
        Devices.Clear();
        foreach (var device in found)
        {
            Devices.Add(device);
        }

        SelectedDevice = Devices.FirstOrDefault();
        SetStatus(Devices.Count == 0 ? "ConnNoDevices" : "ConnDevicesFound");
        Logger.LogInformation("Discovery found {Count} device(s).", Devices.Count);
    });

    // Engine flow (src/Diagnostic.Core/README.md): ConnectAsync(id), then StartSessionAsync(same id).
    private Task ConnectAsync() => RunBusyAsync(async () =>
    {
        var device = SelectedDevice;
        if (device is null)
        {
            return;
        }

        var connected = await _adapter.ConnectAsync(device.AdapterId, CancellationToken.None);
        if (connected.Status != OperationStatus.Success)
        {
            SetResultStatus(connected);
            return;
        }

        var started = await _diagnostics.StartSessionAsync(device.AdapterId, null, CancellationToken.None);
        if (started.Status == OperationStatus.Success && started.Data is not null)
        {
            Session.SetSession(started.Data);
            SetStatus("ConnConnected");
            Logger.LogInformation(
                "Session started on {Adapter} (simulated: {Simulated}).", device.DisplayName, started.Data.IsSimulated);
            return;
        }

        // Do not leave the adapter connected without a session.
        await _adapter.DisconnectAsync(CancellationToken.None);
        SetResultStatus(started);
    });

    private Task DisconnectAsync() => RunBusyAsync(async () =>
    {
        var result = await _adapter.DisconnectAsync(CancellationToken.None);
        Session.Clear();
        if (result.Status == OperationStatus.Success)
        {
            SetStatus("ConnDisconnected");
        }
        else
        {
            SetResultStatus(result);
        }

        Logger.LogInformation("Disconnected ({Status}).", result.Status);
    });
}

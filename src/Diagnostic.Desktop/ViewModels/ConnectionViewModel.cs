using System.Collections.ObjectModel;
using Adapters.Abstractions;
using Contracts;
using Diagnostic.Desktop.Mvvm;
using Diagnostic.Desktop.Services;

namespace Diagnostic.Desktop.ViewModels;

public sealed class ConnectionViewModel : OperationScreenViewModel
{
    private readonly IVciAdapter _adapter;
    private readonly IDiagnosticService _diagnostics;
    private AdapterInfo? _selectedDevice;

    public ConnectionViewModel(IVciAdapter adapter, IDiagnosticService diagnostics, AppSession session)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        Session = session ?? throw new ArgumentNullException(nameof(session));

        DiscoverCommand = new AsyncRelayCommand(DiscoverAsync, () => !IsBusy);
        ConnectCommand = new AsyncRelayCommand(
            ConnectAsync, () => !IsBusy && SelectedDevice is not null && !Session.IsActive);
        DisconnectCommand = new AsyncRelayCommand(DisconnectAsync, () => !IsBusy && Session.IsActive);
    }

    public AppSession Session { get; }

    public ObservableCollection<AdapterInfo> Devices { get; } = new();

    public AdapterInfo? SelectedDevice
    {
        get => _selectedDevice;
        set => SetProperty(ref _selectedDevice, value);
    }

    public AsyncRelayCommand DiscoverCommand { get; }

    public AsyncRelayCommand ConnectCommand { get; }

    public AsyncRelayCommand DisconnectCommand { get; }

    // Discovery only lists devices. Connecting always needs an explicit user action.
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
    });

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

        var session = await _diagnostics.StartSessionAsync(device.AdapterId, null, CancellationToken.None);
        if (session.Status == OperationStatus.Success && session.Data is not null)
        {
            Session.SetSession(session.Data);
            SetStatus("ConnConnected");
            return;
        }

        // Do not leave the adapter connected without a session.
        await _adapter.DisconnectAsync(CancellationToken.None);
        SetResultStatus(session);
    });

    private Task DisconnectAsync() => RunBusyAsync(async () =>
    {
        await _adapter.DisconnectAsync(CancellationToken.None);
        Session.Clear();
        SetStatus("ConnDisconnected");
    });
}

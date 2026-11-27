using Contracts;
using Diagnostic.Desktop.Mock;
using Diagnostic.Desktop.ViewModels;

namespace Desktop.Tests;

public class ConnectionViewModelTests
{
    [Fact]
    public async Task Discover_ListsDevices_WithoutConnecting()
    {
        var h = new Harness();
        var vm = new ConnectionViewModel(h.Adapter, h.Diagnostics, h.Session);

        await vm.DiscoverCommand.ExecuteAsync();

        Assert.Single(vm.Devices);
        Assert.False(h.Adapter.IsConnected);
        Assert.False(h.Session.IsActive);
        Assert.Equal("ConnDevicesFound", vm.StatusKey);
    }

    [Fact]
    public void Connect_IsDisabled_UntilADeviceIsSelected()
    {
        var h = new Harness();
        var vm = new ConnectionViewModel(h.Adapter, h.Diagnostics, h.Session);

        Assert.False(vm.ConnectCommand.CanExecute(null));
    }

    [Fact]
    public async Task Connect_StartsSimulatedSession()
    {
        var h = new Harness();

        await h.ConnectAsync();

        Assert.True(h.Adapter.IsConnected);
        Assert.True(h.Session.IsActive);
        Assert.True(h.Session.IsSimulated);
    }

    [Fact]
    public async Task Disconnect_ClearsSession()
    {
        var h = new Harness();
        var vm = new ConnectionViewModel(h.Adapter, h.Diagnostics, h.Session);
        await vm.DiscoverCommand.ExecuteAsync();
        await vm.ConnectCommand.ExecuteAsync();

        await vm.DisconnectCommand.ExecuteAsync();

        Assert.False(h.Adapter.IsConnected);
        Assert.False(h.Session.IsActive);
        Assert.Equal("ConnDisconnected", vm.StatusKey);
    }

    [Fact]
    public async Task Connect_Timeout_ShowsStatusAndStartsNoSession()
    {
        var adapter = new TimeoutAdapter();
        var session = new Diagnostic.Desktop.Services.AppSession();
        var vm = new ConnectionViewModel(adapter, new UiMockDiagnosticService(adapter), session);
        await vm.DiscoverCommand.ExecuteAsync();

        await vm.ConnectCommand.ExecuteAsync();

        Assert.False(session.IsActive);
        Assert.Equal($"OpStatus_{OperationStatus.Timeout}", vm.StatusKey);
        Assert.Equal("no answer", vm.StatusDetail);
    }
}

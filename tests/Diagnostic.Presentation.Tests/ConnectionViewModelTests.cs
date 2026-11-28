using Contracts;
using Diagnostic.Presentation.ViewModels;

namespace Diagnostic.Presentation.Tests;

public class ConnectionViewModelTests
{
    [Fact]
    public async Task Discover_ListsTheEnginesDevices_WithoutConnecting()
    {
        var h = new Harness();
        var vm = h.CreateConnection();

        await vm.DiscoverCommand.ExecuteAsync();

        Assert.Equal(2, vm.Devices.Count);
        Assert.False(h.Mock.Adapter.IsConnected);
        Assert.False(h.Session.IsActive);
        Assert.Equal("ConnDevicesFound", vm.StatusKey);
    }

    [Fact]
    public void Connect_IsDisabled_UntilADeviceIsSelected()
    {
        var h = new Harness();
        var vm = h.CreateConnection();

        Assert.False(vm.ConnectCommand.CanExecute(null));
    }

    [Fact]
    public async Task Connect_StartsASimulatedSession()
    {
        var h = new Harness();

        await h.ConnectAsync();

        Assert.True(h.Mock.Adapter.IsConnected);
        Assert.True(h.Session.IsSimulated);
        Assert.Contains("SIMULATED", h.Session.Info!.Adapter.DisplayName);
    }

    [Fact]
    public async Task Disconnect_EndsTheSession_AndAReconnectStartsANewOne()
    {
        var h = new Harness();
        var vm = h.CreateConnection();
        await vm.DiscoverCommand.ExecuteAsync();
        await vm.ConnectCommand.ExecuteAsync();
        var first = h.Session.Info!.SessionId;

        await vm.DisconnectCommand.ExecuteAsync();

        Assert.False(h.Mock.Adapter.IsConnected);
        Assert.False(h.Session.IsActive);
        Assert.Equal("ConnDisconnected", vm.StatusKey);

        await vm.ConnectCommand.ExecuteAsync();

        Assert.True(h.Session.IsActive);
        Assert.NotEqual(first, h.Session.Info!.SessionId);
    }

    [Fact]
    public async Task Connect_Timeout_ShowsTheEnginesStatus_AndStartsNoSession()
    {
        var h = new Harness();
        var session = new AppSession();
        var vm = new ConnectionViewModel(new TimeoutAdapter(), h.Diagnostics, session);
        await vm.DiscoverCommand.ExecuteAsync();

        await vm.ConnectCommand.ExecuteAsync();

        Assert.False(session.IsActive);
        Assert.Equal($"OpStatus_{OperationStatus.Timeout}", vm.StatusKey);
        Assert.Equal("no answer", vm.StatusDetail);
    }
}

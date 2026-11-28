using Adapters.Mock;
using Contracts;
using Diagnostic.Presentation.ViewModels;

namespace Diagnostic.Presentation.Tests;

public class ScreenViewModelTests
{
    [Fact]
    public void Scan_IsDisabled_WithoutSession()
    {
        var h = new Harness();
        var vm = new ScanViewModel(h.Diagnostics, h.Session);

        Assert.False(vm.RunCommand.CanExecute(null));
    }

    [Fact]
    public async Task Vehicle_Read_ShowsTheVinTheEngineReturned_AndDashesForTheRest()
    {
        var h = new Harness();
        await h.ConnectAsync();
        var vm = new VehicleViewModel(h.Diagnostics, h.Session);
        Assert.Equal("—", vm.VinText);

        await vm.ReadCommand.ExecuteAsync();

        Assert.Equal("SMLTD0000TEST0001", vm.VinText);
        Assert.Equal("VinFormatOk", vm.VinStatusKey);
        Assert.Equal("—", vm.MakeText);
        Assert.Equal("—", vm.YearText);
    }

    [Fact]
    public async Task Vehicle_Read_OnAnEcuWithoutVin_ShowsNoVinAndTheEnginesStatus()
    {
        var h = new Harness();
        await h.ConnectAsync(1);
        var vm = new VehicleViewModel(h.Diagnostics, h.Session);

        await vm.ReadCommand.ExecuteAsync();

        Assert.Equal("—", vm.VinText);
        Assert.NotEqual("OpStatus_Success", vm.StatusKey);
        Assert.StartsWith("OpStatus_", vm.StatusKey);
    }

    [Fact]
    public async Task Scan_ShowsTheEnginesEcu_AndStoresItInTheSession()
    {
        var h = new Harness();
        await h.ConnectAsync();
        var vm = new ScanViewModel(h.Diagnostics, h.Session);

        await vm.RunCommand.ExecuteAsync();

        var row = Assert.Single(vm.Rows);
        Assert.Equal("obd2", row.EcuId);
        Assert.Equal("EcuStatus_Responded", row.StatusKey);
        Assert.Equal(3, row.DtcCount);
        Assert.Single(h.Session.LastScan);
        Assert.Equal("OpStatus_Success", vm.StatusKey);
    }

    [Fact]
    public async Task Scan_WhenTheEcuFails_StillShowsTheRowWithAnErrorStatus_AndKeepsTheSession()
    {
        var h = new Harness();
        await h.ConnectAsync();
        h.Mock.Adapter.Fault = MockFault.TransportError;
        var vm = new ScanViewModel(h.Diagnostics, h.Session);

        await vm.RunCommand.ExecuteAsync();

        var row = Assert.Single(vm.Rows);
        Assert.Equal("EcuStatus_Error", row.StatusKey);
        Assert.Equal($"OpStatus_{OperationStatus.TransportError}", vm.StatusKey);
        Assert.True(h.Session.IsActive);
    }

    [Fact]
    public async Task Scan_WhenTheConnectionIsLost_EndsTheSessionInTheUi()
    {
        var h = new Harness();
        await h.ConnectAsync();
        h.Mock.Adapter.Fault = MockFault.Disconnect;
        var vm = new ScanViewModel(h.Diagnostics, h.Session);

        await vm.RunCommand.ExecuteAsync();

        Assert.False(h.Session.IsActive);
        Assert.Equal($"OpStatus_{OperationStatus.NotConnected}", vm.StatusKey);
        Assert.Empty(vm.Rows);
    }

    [Fact]
    public async Task Dtc_ReadsStoredAndPendingCodes()
    {
        var h = new Harness();
        await h.ConnectAsync();
        var vm = new DtcViewModel(h.Diagnostics, h.Session);
        Assert.False(vm.HasScan);

        await new ScanViewModel(h.Diagnostics, h.Session).RunCommand.ExecuteAsync();

        Assert.True(vm.HasScan);
        Assert.Equal("obd2", vm.SelectedEcu?.EcuId);

        await vm.ReadCommand.ExecuteAsync();

        Assert.Equal(3, vm.Dtcs.Count);
        Assert.Contains(vm.Dtcs, dtc => dtc.Code == "P0300" && dtc.IsConfirmed && !dtc.IsPending);
        Assert.Contains(vm.Dtcs, dtc => dtc.Code == "P0171" && dtc.IsPending && !dtc.IsConfirmed);
        Assert.Equal("OpStatus_Success", vm.StatusKey);
    }

    [Fact]
    public async Task Dtc_PartialSuccessWithNoCodes_IsNeverReportedAsNoCodes()
    {
        // The limited vehicle rejects the pending-DTC service, so the engine answers PartialSuccess.
        var h = new Harness();
        await h.ConnectAndScanAsync(1);
        var vm = new DtcViewModel(h.Diagnostics, h.Session);

        await vm.ReadCommand.ExecuteAsync();

        Assert.Empty(vm.Dtcs);
        Assert.Equal("OpStatus_PartialSuccess", vm.StatusKey);
        Assert.NotEqual("DtcNone", vm.StatusKey);
        Assert.False(string.IsNullOrEmpty(vm.StatusDetail));
    }

    [Fact]
    public async Task Disconnect_ClearsTheScanAndTheEcuLists()
    {
        var h = new Harness();
        await h.ConnectAndScanAsync();
        var vm = new DtcViewModel(h.Diagnostics, h.Session);
        Assert.NotEmpty(vm.AvailableEcus);

        h.Session.Clear();

        Assert.Empty(vm.AvailableEcus);
        Assert.False(vm.HasScan);
        Assert.Null(vm.SelectedEcu);
    }
}

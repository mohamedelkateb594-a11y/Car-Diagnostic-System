using Contracts;
using Diagnostic.Desktop.Services;
using Diagnostic.Desktop.ViewModels;

namespace Desktop.Tests;

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
    public async Task Scan_ShowsEveryEcuWithItsStatus_AndStoresResultInSession()
    {
        var h = new Harness();
        await h.ConnectAsync();
        var vm = new ScanViewModel(h.Diagnostics, h.Session);

        await vm.RunCommand.ExecuteAsync();

        Assert.Equal(4, vm.Rows.Count);
        Assert.Equal(4, h.Session.LastScan.Count);
        Assert.Contains(vm.Rows, row => row.StatusKey == $"EcuStatus_{EcuScanStatus.NoResponse}");
        Assert.Equal($"OpStatus_{OperationStatus.PartialSuccess}", vm.StatusKey);
    }

    [Fact]
    public async Task Vehicle_Read_FillsIdentityFromService()
    {
        var h = new Harness();
        await h.ConnectAsync();
        var vm = new VehicleViewModel(h.Diagnostics, h.Session);
        Assert.Equal("—", vm.VinText);

        await vm.ReadCommand.ExecuteAsync();

        Assert.NotEqual("—", vm.VinText);
        Assert.Equal("VinFormatOk", vm.VinStatusKey);
        Assert.Equal("2020", vm.YearText);
    }

    [Fact]
    public async Task Dtc_OffersOnlyEcusThatResponded_AndReadsTheirCodes()
    {
        var h = new Harness();
        await h.ConnectAsync();
        var vm = new DtcViewModel(h.Diagnostics, h.Session);
        Assert.False(vm.HasScan);

        await new ScanViewModel(h.Diagnostics, h.Session).RunCommand.ExecuteAsync();

        Assert.True(vm.HasScan);
        Assert.Equal(2, vm.AvailableEcus.Count);
        Assert.Equal("ecm", vm.SelectedEcu?.EcuId);

        await vm.ReadCommand.ExecuteAsync();

        Assert.Equal(2, vm.Dtcs.Count);
        Assert.Contains(vm.Dtcs, dtc => dtc.Code == "P0300" && dtc.IsConfirmed);
        Assert.Contains(vm.Dtcs, dtc => dtc.Code == "P0420" && dtc.IsPending);
    }

    [Fact]
    public async Task Dtc_EcuWithoutCodes_ReportsNone()
    {
        var h = new Harness();
        await h.ConnectAndScanAsync();
        var vm = new DtcViewModel(h.Diagnostics, h.Session);
        vm.SelectedEcu = vm.AvailableEcus.Single(ecu => ecu.EcuId == "tcm");

        await vm.ReadCommand.ExecuteAsync();

        Assert.Empty(vm.Dtcs);
        Assert.Equal("DtcNone", vm.StatusKey);
    }

    [Fact]
    public async Task Disconnect_ClearsScanAndEcuLists()
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

    [Theory]
    [InlineData("TEST0000000000001", true)]
    [InlineData("ABCDEFGHJKLMNPRST", true)]
    [InlineData("1HGBH41JXMN10918I", false)]
    [InlineData("SHORT", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void VinValidator_ChecksFormatOnly(string? vin, bool expected)
        => Assert.Equal(expected, VinValidator.IsWellFormed(vin));
}

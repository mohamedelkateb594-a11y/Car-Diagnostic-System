using Diagnostic.Presentation.ViewModels;

namespace Diagnostic.Presentation.Tests;

public class LiveDataViewModelTests
{
    [Fact]
    public async Task PollOnce_ShowsTheParametersTheEngineSupports_AndFlagsSimulatedData()
    {
        var h = new Harness();
        await h.ConnectAndScanAsync();
        var vm = new LiveDataViewModel(h.Diagnostics, h.Session);

        Assert.True(await vm.PollOnceAsync(CancellationToken.None));
        Assert.True(await vm.PollOnceAsync(CancellationToken.None));

        Assert.NotEmpty(vm.Series);
        Assert.All(vm.Series, series => Assert.Equal(2, series.Values.Count));
        Assert.True(vm.HasSimulatedData);
    }

    [Fact]
    public async Task PollOnce_UnknownParameter_ShowsTheEnginesStatus_AndStopsPolling()
    {
        var h = new Harness();
        await h.ConnectAndScanAsync();
        var vm = new LiveDataViewModel(h.Diagnostics, h.Session) { ParameterIdsText = "does_not_exist" };

        var keepPolling = await vm.PollOnceAsync(CancellationToken.None);

        Assert.False(keepPolling);
        Assert.Empty(vm.Series);
        Assert.Equal("OpStatus_Unsupported", vm.StatusKey);
    }

    [Fact]
    public async Task Series_IsBounded()
    {
        var h = new Harness();
        await h.ConnectAndScanAsync();
        var vm = new LiveDataViewModel(h.Diagnostics, h.Session);

        for (var i = 0; i < LiveSeriesViewModel.MaxPoints + 5; i++)
        {
            Assert.True(await vm.PollOnceAsync(CancellationToken.None));
        }

        Assert.All(vm.Series, series => Assert.Equal(LiveSeriesViewModel.MaxPoints, series.Values.Count));
    }

    [Fact]
    public async Task StartAndStop_PollsUntilStopped()
    {
        var h = new Harness();
        await h.ConnectAndScanAsync();
        var vm = new LiveDataViewModel(h.Diagnostics, h.Session) { PollInterval = TimeSpan.FromMilliseconds(10) };

        var run = vm.StartCommand.ExecuteAsync();
        await Harness.WaitUntilAsync(() => vm.Series.Count > 0 && vm.Series[0].Values.Count >= 2);
        Assert.True(vm.IsRunning);

        vm.Stop();
        await run;

        Assert.False(vm.IsRunning);
    }
}

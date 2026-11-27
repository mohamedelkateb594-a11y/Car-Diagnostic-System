using Contracts;
using Diagnostic.Desktop.Mock;

namespace Desktop.Tests;

public class UiMockServicesTests
{
    [Fact]
    public async Task StartSession_BeforeAdapterConnects_ReturnsNotConnected()
    {
        var h = new Harness();

        var result = await h.Diagnostics.StartSessionAsync(UiMockVciAdapter.AdapterId, null, CancellationToken.None);

        Assert.Equal(OperationStatus.NotConnected, result.Status);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task EveryMockResult_IsMarkedSimulated()
    {
        var h = new Harness();
        await h.ConnectAsync();
        var id = h.Session.Info!.SessionId;

        Assert.True(h.Session.Info.IsSimulated);
        Assert.True((await h.Diagnostics.ReadVehicleIdentityAsync(id, CancellationToken.None)).IsSimulated);
        Assert.True((await h.Diagnostics.FullScanAsync(id, CancellationToken.None)).IsSimulated);

        var live = await h.Diagnostics.ReadLiveDataAsync(id, "ecm", Array.Empty<string>(), CancellationToken.None);
        Assert.All(live.Data!, point => Assert.True(point.IsSimulated));
    }

    [Fact]
    public async Task ReadDtcs_ForEcuThatDidNotRespond_IsNotApplicable()
    {
        var h = new Harness();
        await h.ConnectAsync();

        var result = await h.Diagnostics.ReadDtcsAsync(h.Session.Info!.SessionId, "abs", CancellationToken.None);

        Assert.Equal(OperationStatus.NotApplicable, result.Status);
    }

    [Fact]
    public async Task Operations_WithUnknownSession_ReturnNotConnected()
    {
        var h = new Harness();
        await h.ConnectAsync();

        var result = await h.Diagnostics.FullScanAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(OperationStatus.NotConnected, result.Status);
    }
}

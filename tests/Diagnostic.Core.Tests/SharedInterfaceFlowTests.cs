using Adapters.Abstractions;
using Adapters.Mock;
using Contracts;

namespace Diagnostic.Core.Tests;

/// <summary>Drives the core the way a UI does: through IVciAdapter and IDiagnosticService.</summary>
public class SharedInterfaceFlowTests
{
    private const string Sedan = "mock-sedan";
    private static readonly CancellationToken None = CancellationToken.None;

    [Fact]
    public async Task Connect_read_disconnect_and_reconnect()
    {
        await using var mock = new MockDiagnosticService();
        IVciAdapter adapter = mock.ManagedAdapter;
        IDiagnosticService diagnostics = mock;

        for (var round = 0; round < 2; round++)
        {
            var devices = await adapter.DiscoverAsync(None);
            Assert.Contains(devices, d => d.AdapterId == Sedan);

            var connected = await adapter.ConnectAsync(Sedan, None);
            Assert.Equal(OperationStatus.Success, connected.Status);
            Assert.True(connected.Data);
            Assert.True(adapter.IsConnected);

            var session = await diagnostics.StartSessionAsync(Sedan, null, None);
            Assert.Equal(OperationStatus.Success, session.Status);
            Assert.NotNull(session.Data);
            var id = session.Data.SessionId;

            var scan = await diagnostics.FullScanAsync(id, None);
            Assert.NotNull(scan.Data);
            var ecu = Assert.Single(scan.Data);

            var live = await diagnostics.ReadLiveDataAsync(id, ecu.EcuId, [], None);
            Assert.Equal(OperationStatus.Success, live.Status);
            Assert.NotNull(live.Data);
            Assert.Equal(SimulatedVehicles.Sedan.LiveData.Count, live.Data.Count);

            var disconnected = await adapter.DisconnectAsync(None);
            Assert.Equal(OperationStatus.Success, disconnected.Status);
            Assert.False(adapter.IsConnected);

            var after = await diagnostics.FullScanAsync(id, None);
            Assert.Equal(OperationStatus.NotConnected, after.Status);
        }
    }

    [Fact]
    public async Task Failed_connection_is_not_reported_as_connected()
    {
        await using var mock = new MockDiagnosticService();

        var result = await mock.ManagedAdapter.ConnectAsync("missing", None);

        Assert.Equal("AdapterNotFound", result.ErrorCode);
        Assert.False(result.Data);
        Assert.False(mock.ManagedAdapter.IsConnected);
    }

    [Fact]
    public async Task Raw_requests_are_refused()
    {
        await using var mock = new MockDiagnosticService();
        await mock.ManagedAdapter.ConnectAsync(Sedan, None);

        var result = await mock.ManagedAdapter.SendAsync([0x04], TimeSpan.FromSeconds(1), None);

        Assert.Equal(OperationStatus.Unsupported, result.Status);
        Assert.Equal("RawAccessNotAllowed", result.ErrorCode);
        Assert.True(result.IsSimulated);
    }

    [Fact]
    public async Task Cancelled_discovery_throws_instead_of_listing_no_device()
    {
        await using var mock = new MockDiagnosticService();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => mock.ManagedAdapter.DiscoverAsync(new CancellationToken(true)));
    }

    [Fact]
    public async Task Empty_parameter_list_with_nothing_supported_is_unsupported()
    {
        await using var mock = new MockDiagnosticService();
        var session = await mock.StartSessionAsync(Sedan, null, None);
        Assert.NotNull(session.Data);
        mock.Adapter.ResponseOverride = _ => [0x41, 0x00, 0x00, 0x00, 0x00, 0x00];

        var result = await mock.ReadLiveDataAsync(session.Data.SessionId, DiagnosticService.Obd2EcuId, [], None);

        Assert.Equal(OperationStatus.Unsupported, result.Status);
        Assert.Equal("ParameterUnavailable", result.ErrorCode);
        Assert.Null(result.Data);
    }
}

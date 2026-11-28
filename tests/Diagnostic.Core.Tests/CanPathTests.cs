using Adapters.IsoTp;
using Adapters.Mock;
using Contracts;
using Diagnostic.Protocols;

namespace Diagnostic.Core.Tests;

/// <summary>Simulated path: Core, IVciAdapter, ISO-TP, CAN frames, simulated ECU.</summary>
public class CanPathTests
{
    private const string SedanId = "mock-sedan-can";
    private static readonly CancellationToken None = CancellationToken.None;
    private static readonly TimeSpan Long = TimeSpan.FromSeconds(5);

    private static VciConnectionManager Sedan(SimulatedUdsData? uds = null) =>
        new(SimulatedCanAdapter.Create(SimulatedVehicles.Sedan, uds));

    private static IsoTpVciAdapter EcuAdapter(Func<byte[], byte[]?> responder) => new(
        new AdapterInfo("can-test", "SIMULATED CAN test adapter", "Test", "Test", "0", []),
        () => new SimulatedCanEcu(0x100, 0x200, responder),
        new IsoTpOptions(0x100, 0x200, false),
        true);

    [Fact]
    public async Task Obd2_workflows_run_over_can()
    {
        await using var manager = Sedan();
        var service = new DiagnosticService(manager);
        var session = await service.StartSessionAsync(SedanId, null, None);
        Assert.NotNull(session.Data);
        Assert.True(session.IsSimulated);
        var id = session.Data.SessionId;

        var identity = await service.ReadVehicleIdentityAsync(id, None);
        var dtcs = await service.ReadDtcsAsync(id, DiagnosticService.Obd2EcuId, None);
        var parameters = await service.GetSupportedParametersAsync(id, DiagnosticService.Obd2EcuId, None);
        Assert.NotNull(parameters.Data);
        var live = await service.ReadLiveDataAsync(
            id, DiagnosticService.Obd2EcuId, parameters.Data.Select(p => p.ParameterId).ToArray(), None);

        Assert.Equal("SMLTD0000TEST0001", identity.Data?.Vin);
        Assert.NotNull(dtcs.Data);
        Assert.Equal("P0100,P0300,P0171", string.Join(',', dtcs.Data.Select(d => d.Code)));
        Assert.Equal(OperationStatus.Success, live.Status);
        Assert.NotNull(live.Data);
        Assert.Equal(SimulatedVehicles.Sedan.LiveData.Count, live.Data.Count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(100)]
    [InlineData(300)]
    public async Task Requests_and_responses_are_segmented_both_ways(int length)
    {
        await using var adapter = EcuAdapter(request => request);
        await adapter.ConnectAsync("can-test", None);
        var payload = Enumerable.Range(0, length).Select(i => (byte)i).ToArray();

        var result = await adapter.SendAsync(payload, Long, None);

        Assert.NotNull(result.Data);
        Assert.Equal(Convert.ToHexString(payload), Convert.ToHexString(result.Data));
        Assert.True(result.IsSimulated);
    }

    [Fact]
    public async Task Silent_ecu_times_out()
    {
        await using var adapter = EcuAdapter(_ => null);
        await adapter.ConnectAsync("can-test", None);

        var result = await adapter.SendAsync([0x03], TimeSpan.FromMilliseconds(100), None);

        Assert.Equal(OperationStatus.Timeout, result.Status);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task Adapter_lifecycle_is_enforced()
    {
        await using var adapter = EcuAdapter(request => request);

        var before = await adapter.SendAsync([0x03], Long, None);
        var unknown = await adapter.ConnectAsync("other", None);
        await adapter.ConnectAsync("can-test", None);
        var busy = await adapter.ConnectAsync("can-test", None);
        await adapter.DisconnectAsync(None);

        Assert.Equal(OperationStatus.NotConnected, before.Status);
        Assert.Equal("AdapterNotFound", unknown.ErrorCode);
        Assert.Equal("AdapterBusy", busy.ErrorCode);
        Assert.False(adapter.IsConnected);
    }

    [Fact]
    public async Task Channel_open_failure_is_reported()
    {
        await using var adapter = new IsoTpVciAdapter(
            new AdapterInfo("broken", "SIMULATED broken adapter", "Test", "Test", "0", []),
            () => throw new IOException("Simulated open failure."),
            new IsoTpOptions(0x100, 0x200, false),
            true);

        var result = await adapter.ConnectAsync("broken", None);

        Assert.Equal(OperationStatus.TransportError, result.Status);
        Assert.False(adapter.IsConnected);
    }

    [Theory]
    [InlineData(0xFF, "P0100-00:True:False|P0171-00:False:True")]
    [InlineData(0x04, "P0171-00:False:True")]
    public async Task Uds_dtcs_are_read_by_status_mask(int mask, string expected)
    {
        await using var manager = Sedan(SimulatedUdsData.Sample);
        await manager.ConnectAsync(SedanId, None);

        var result = await new UdsReader(manager).ReadDtcsByStatusMaskAsync((byte)mask, None);

        Assert.True(result.IsSimulated);
        Assert.NotNull(result.Data);
        Assert.Equal(
            expected,
            string.Join('|', result.Data.Select(d => $"{d.Code}:{d.IsConfirmed}:{d.IsPending}")));
    }

    [Fact]
    public async Task Uds_data_identifier_is_read_and_unknown_one_is_rejected()
    {
        await using var manager = Sedan(SimulatedUdsData.Sample);
        await manager.ConnectAsync(SedanId, None);
        var reader = new UdsReader(manager);

        var known = await reader.ReadDataByIdentifierAsync(0x1234, None);
        var unknown = await reader.ReadDataByIdentifierAsync(0x9999, None);

        Assert.NotNull(known.Data);
        Assert.Equal("53494D", Convert.ToHexString(known.Data));
        Assert.Equal(OperationStatus.Failed, unknown.Status);
        Assert.Equal("7F 22 31", unknown.Message);
        Assert.Null(unknown.Data);
    }

    [Fact]
    public async Task Ecu_without_uds_reports_unsupported()
    {
        await using var manager = Sedan();
        await manager.ConnectAsync(SedanId, None);

        var result = await new UdsReader(manager).ReadDtcsByStatusMaskAsync(0xFF, None);

        Assert.Equal(OperationStatus.Unsupported, result.Status);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task Uds_read_without_connection_is_not_connected()
    {
        await using var manager = Sedan(SimulatedUdsData.Sample);

        var result = await new UdsReader(manager).ReadDataByIdentifierAsync(0x1234, None);

        Assert.Equal(OperationStatus.NotConnected, result.Status);
    }
}

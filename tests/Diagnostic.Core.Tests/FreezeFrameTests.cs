using Adapters.Mock;
using Contracts;

namespace Diagnostic.Core.Tests;

public class FreezeFrameTests
{
    private const string Ecu = DiagnosticService.Obd2EcuId;
    private static readonly CancellationToken None = CancellationToken.None;

    private static async Task<Guid> StartAsync(MockDiagnosticService service, SimulatedFreezeFrame frame)
    {
        service.Adapter.ResponseOverride = frame.Respond;
        var session = await service.StartSessionAsync("mock-sedan", null, None);
        Assert.NotNull(session.Data);
        return session.Data.SessionId;
    }

    [Fact]
    public async Task Partial_frame_returns_available_values_and_reports_the_rest()
    {
        await using var service = new MockDiagnosticService();
        var id = await StartAsync(service, SimulatedFreezeFrame.Sample);

        var result = await service.ReadFreezeFrameAsync(id, Ecu, null, None);

        Assert.Equal(OperationStatus.PartialSuccess, result.Status);
        Assert.True(result.IsSimulated);
        Assert.NotNull(result.Data);
        Assert.Equal("P0300", result.Data.DtcCode);
        Assert.Null(result.Data.CapturedAtUtc);
        Assert.Equal(
            "obd2.01.05=80|obd2.01.0C=2000|obd2.01.0D=50",
            string.Join('|', result.Data.Values.Select(v => $"{v.ParameterId}={v.Value:0}")));
        Assert.Contains("obd2.01.42", result.Message);
    }

    [Fact]
    public async Task Complete_frame_is_success_for_matching_code()
    {
        await using var service = new MockDiagnosticService();
        var id = await StartAsync(service, new SimulatedFreezeFrame(0x0100, SimulatedVehicles.Sedan.LiveData));

        var result = await service.ReadFreezeFrameAsync(id, Ecu, "p0100", None);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(SimulatedVehicles.Sedan.LiveData.Count, result.Data.Values.Count);
    }

    [Fact]
    public async Task No_stored_frame_is_success_without_data()
    {
        await using var service = new MockDiagnosticService();
        var id = await StartAsync(service, SimulatedFreezeFrame.Sample with { Dtc = 0 });

        var result = await service.ReadFreezeFrameAsync(id, Ecu, null, None);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task Other_code_has_no_frame()
    {
        await using var service = new MockDiagnosticService();
        var id = await StartAsync(service, SimulatedFreezeFrame.Sample);

        var result = await service.ReadFreezeFrameAsync(id, Ecu, "P0171", None);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Null(result.Data);
        Assert.Contains("P0300", result.Message);
    }

    [Fact]
    public async Task Unknown_ecu_is_unsupported()
    {
        await using var service = new MockDiagnosticService();
        var id = await StartAsync(service, SimulatedFreezeFrame.Sample);

        var result = await service.ReadFreezeFrameAsync(id, "engine", null, None);

        Assert.Equal("UnknownEcu", result.ErrorCode);
    }
}

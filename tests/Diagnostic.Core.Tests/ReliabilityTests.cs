using Adapters.Mock;
using Contracts;

namespace Diagnostic.Core.Tests;

public class ReliabilityTests
{
    private const string Ecu = DiagnosticService.Obd2EcuId;
    private static readonly CancellationToken None = CancellationToken.None;

    private static async Task<Guid> StartAsync(MockDiagnosticService service)
    {
        var session = await service.StartSessionAsync("mock-sedan", null, None);
        Assert.NotNull(session.Data);
        return session.Data.SessionId;
    }

    [Fact]
    public async Task Live_data_stops_asking_a_silent_ecu()
    {
        await using var service = new MockDiagnosticService(
            options: new DiagnosticOptions { RequestTimeout = TimeSpan.FromMilliseconds(100) });
        var id = await StartAsync(service);
        await service.GetSupportedParametersAsync(id, Ecu, None);
        service.Adapter.Fault = MockFault.NoResponse;

        var result = await service.ReadLiveDataAsync(id, Ecu, [], None);

        Assert.Equal(OperationStatus.Timeout, result.Status);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
        Assert.Contains("obd2.01.0B: Timeout", result.Message);
        Assert.Contains("obd2.01.0C: not read", result.Message);
        Assert.Contains("obd2.01.42: not read", result.Message);
    }

    [Fact]
    public async Task Service_sends_only_read_services()
    {
        await using var service = new MockDiagnosticService();
        var sent = new List<byte>();
        service.Adapter.ResponseOverride = request =>
        {
            sent.Add(request[0]);
            return SimulatedFreezeFrame.Sample.Respond(request);
        };
        var id = await StartAsync(service);

        await service.ReadVehicleIdentityAsync(id, None);
        await service.FullScanAsync(id, None);
        await service.ReadDtcsAsync(id, Ecu, None);
        await service.ReadLiveDataAsync(id, Ecu, [], None);
        await service.ReadFreezeFrameAsync(id, Ecu, null, None);
        await service.EndSessionAsync(id, None);

        byte[] readServices = [0x01, 0x02, 0x03, 0x07, 0x09];
        Assert.NotEmpty(sent);
        Assert.All(sent, s => Assert.Contains(s, readServices));
    }
}

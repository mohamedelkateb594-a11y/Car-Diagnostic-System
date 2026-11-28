using Adapters.Mock;
using Contracts;

namespace Diagnostic.Core.Tests;

public class DiagnosticServiceTests
{
    private const string Ecu = DiagnosticService.Obd2EcuId;
    private static readonly CancellationToken None = CancellationToken.None;

    private static async Task<Guid> StartAsync(MockDiagnosticService service, string adapterId = "mock-sedan")
    {
        var session = await service.StartSessionAsync(adapterId, null, None);
        Assert.Equal(OperationStatus.Success, session.Status);
        Assert.NotNull(session.Data);
        Assert.True(session.Data.IsSimulated);
        return session.Data.SessionId;
    }

    [Fact]
    public async Task Vin_is_read_and_marked_simulated()
    {
        await using var service = new MockDiagnosticService();
        var id = await StartAsync(service);

        var result = await service.ReadVehicleIdentityAsync(id, None);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.True(result.IsSimulated);
        Assert.Equal("SMLTD0000TEST0001", result.Data?.Vin);
        Assert.Null(result.Data?.Make);
    }

    [Fact]
    public async Task Missing_vin_is_unsupported_not_fake()
    {
        await using var service = new MockDiagnosticService();
        var id = await StartAsync(service, "mock-limited");

        var result = await service.ReadVehicleIdentityAsync(id, None);

        Assert.Equal(OperationStatus.Unsupported, result.Status);
        Assert.Equal("NegativeResponse", result.ErrorCode);
        Assert.Equal("7F 09 12", result.Message);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task Full_scan_returns_stored_and_pending_codes()
    {
        await using var service = new MockDiagnosticService();
        var id = await StartAsync(service);

        var result = await service.FullScanAsync(id, None);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.NotNull(result.Data);
        var ecu = Assert.Single(result.Data);
        Assert.Equal(EcuScanStatus.Responded, ecu.Status);
        Assert.Equal(
            "P0100:stored|P0300:stored|P0171:pending",
            string.Join('|', ecu.Dtcs.Select(d => $"{d.Code}:{d.Status}")));
        Assert.All(ecu.Dtcs, d => Assert.Null(d.Description));
    }

    [Fact]
    public async Task Rejected_pending_service_gives_partial_success()
    {
        await using var service = new MockDiagnosticService();
        var id = await StartAsync(service, "mock-limited");

        var result = await service.ReadDtcsAsync(id, Ecu, None);

        Assert.Equal(OperationStatus.PartialSuccess, result.Status);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
        Assert.Contains("7F 07 11", result.Message);
    }

    [Fact]
    public async Task Unknown_ecu_is_unsupported()
    {
        await using var service = new MockDiagnosticService();
        var id = await StartAsync(service);

        var result = await service.ReadDtcsAsync(id, "engine", None);

        Assert.Equal(OperationStatus.Unsupported, result.Status);
        Assert.Equal("UnknownEcu", result.ErrorCode);
    }

    [Fact]
    public async Task Supported_parameters_follow_all_bitmap_pages()
    {
        await using var service = new MockDiagnosticService();
        var id = await StartAsync(service);

        var result = await service.GetSupportedParametersAsync(id, Ecu, None);

        Assert.NotNull(result.Data);
        Assert.Equal(SimulatedVehicles.Sedan.LiveData.Count, result.Data.Count);
        Assert.Contains(result.Data, p => p.ParameterId == "obd2.01.42");
    }

    [Fact]
    public async Task Live_data_is_decoded()
    {
        await using var service = new MockDiagnosticService();
        var id = await StartAsync(service);

        var result = await service.ReadLiveDataAsync(id, Ecu, ["obd2.01.0C", "obd2.01.05"], None);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(800.0, result.Data[0].Value, 3);
        Assert.Equal(83.0, result.Data[1].Value, 3);
        Assert.All(result.Data, p => Assert.True(p.IsSimulated));
    }

    [Fact]
    public async Task Unavailable_parameters_are_omitted_and_reported()
    {
        await using var service = new MockDiagnosticService();
        var id = await StartAsync(service);

        var partial = await service.ReadLiveDataAsync(id, Ecu, ["obd2.01.0C", "nope", "obd2.01.5C"], None);
        var none = await service.ReadLiveDataAsync(id, Ecu, ["obd2.01.5C"], None);
        var missing = await service.ReadLiveDataAsync(id, Ecu, null!, None);

        Assert.Equal(OperationStatus.PartialSuccess, partial.Status);
        Assert.NotNull(partial.Data);
        Assert.Single(partial.Data);
        Assert.Contains("nope", partial.Message);
        Assert.Equal(OperationStatus.Unsupported, none.Status);
        Assert.NotNull(none.Data);
        Assert.Empty(none.Data);
        Assert.Equal("InvalidArgument", missing.ErrorCode);
    }

    [Fact]
    public async Task Rejected_parameter_is_not_returned_as_a_value()
    {
        await using var service = new MockDiagnosticService();
        var id = await StartAsync(service);
        service.Adapter.ResponseOverride = request =>
            request.Length == 2 && request[1] == 0x0C ? [0x7F, 0x01, 0x31] : null;

        var result = await service.ReadLiveDataAsync(id, Ecu, ["obd2.01.0C"], None);

        Assert.Equal(OperationStatus.Failed, result.Status);
        Assert.Equal("NegativeResponse", result.ErrorCode);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task Freeze_frame_is_unsupported()
    {
        await using var service = new MockDiagnosticService();
        var id = await StartAsync(service);

        var result = await service.ReadFreezeFrameAsync(id, Ecu, null, None);

        Assert.Equal(OperationStatus.Unsupported, result.Status);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task Only_one_session_can_be_active()
    {
        await using var service = new MockDiagnosticService();
        await StartAsync(service);

        var second = await service.StartSessionAsync("mock-sedan", null, None);

        Assert.Equal("SessionActive", second.ErrorCode);
    }

    [Fact]
    public async Task Ending_a_session_disconnects_and_invalidates_it()
    {
        await using var service = new MockDiagnosticService();
        var id = await StartAsync(service);

        var end = await service.EndSessionAsync(id, None);
        var after = await service.ReadVehicleIdentityAsync(id, None);

        Assert.Equal(OperationStatus.Success, end.Status);
        Assert.False(service.Adapter.IsConnected);
        Assert.Equal("UnknownSession", after.ErrorCode);
    }

    [Fact]
    public async Task Old_session_cannot_use_a_new_connection()
    {
        await using var service = new MockDiagnosticService();
        var id = await StartAsync(service);
        await service.Connection.DisconnectAsync(None);

        var disconnected = await service.FullScanAsync(id, None);
        await service.Connection.ConnectAsync("mock-limited", None);
        var reconnected = await service.FullScanAsync(id, None);

        Assert.Equal(OperationStatus.NotConnected, disconnected.Status);
        Assert.Equal(OperationStatus.NotConnected, reconnected.Status);
    }

    [Theory]
    [InlineData(MockFault.NoResponse, OperationStatus.Timeout)]
    [InlineData(MockFault.TransportError, OperationStatus.TransportError)]
    [InlineData(MockFault.MalformedResponse, OperationStatus.InvalidResponse)]
    [InlineData(MockFault.Throw, OperationStatus.TransportError)]
    public async Task Adapter_faults_become_structured_results(MockFault fault, OperationStatus expected)
    {
        await using var service = new MockDiagnosticService(
            options: new DiagnosticOptions { RequestTimeout = TimeSpan.FromMilliseconds(100) });
        var id = await StartAsync(service);
        service.Adapter.Fault = fault;

        var result = await service.ReadVehicleIdentityAsync(id, None);

        Assert.Equal(expected, result.Status);
        Assert.Null(result.Data);
        Assert.True(result.IsSimulated);
    }

    [Fact]
    public async Task Silent_ecu_is_reported_as_no_response()
    {
        await using var service = new MockDiagnosticService(
            options: new DiagnosticOptions { RequestTimeout = TimeSpan.FromMilliseconds(100) });
        var id = await StartAsync(service);
        service.Adapter.Fault = MockFault.NoResponse;

        var result = await service.FullScanAsync(id, None);

        Assert.Equal(OperationStatus.Timeout, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(EcuScanStatus.NoResponse, Assert.Single(result.Data).Status);
    }

    [Fact]
    public async Task Lost_connection_is_reported()
    {
        await using var service = new MockDiagnosticService();
        var id = await StartAsync(service);
        service.Adapter.Fault = MockFault.Disconnect;

        var first = await service.ReadVehicleIdentityAsync(id, None);
        var second = await service.ReadVehicleIdentityAsync(id, None);

        Assert.Equal("ConnectionLost", first.ErrorCode);
        Assert.Equal(OperationStatus.NotConnected, second.Status);
    }

    [Fact]
    public async Task Cancellation_during_a_request_is_reported()
    {
        await using var service = new MockDiagnosticService(
            options: new DiagnosticOptions { RequestTimeout = TimeSpan.FromSeconds(5) });
        var id = await StartAsync(service);
        service.Adapter.ResponseDelay = TimeSpan.FromSeconds(2);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var result = await service.FullScanAsync(id, cts.Token);

        Assert.Equal(OperationStatus.Cancelled, result.Status);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task Mock_adapter_refuses_requests_before_connecting()
    {
        await using var adapter = new MockVciAdapter();

        var result = await adapter.SendAsync([0x03], TimeSpan.FromSeconds(1), None);

        Assert.Equal(OperationStatus.NotConnected, result.Status);
        Assert.True(result.IsSimulated);
    }
}

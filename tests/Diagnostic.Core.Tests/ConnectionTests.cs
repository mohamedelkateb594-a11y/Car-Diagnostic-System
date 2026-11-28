using Adapters.Abstractions;
using Adapters.Mock;
using Contracts;

namespace Diagnostic.Core.Tests;

public class ConnectionTests
{
    private static readonly CancellationToken None = CancellationToken.None;
    private static readonly DiagnosticOptions Fast = new()
    {
        RequestTimeout = TimeSpan.FromMilliseconds(100),
        ConnectionTimeout = TimeSpan.FromMilliseconds(200),
        AdapterGracePeriod = TimeSpan.FromMilliseconds(100),
    };

    [Fact]
    public async Task Discovery_lists_simulated_adapters_without_connecting()
    {
        await using var service = new MockDiagnosticService();

        var result = await service.Connection.DiscoverAsync(None);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.True(result.IsSimulated);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Count);
        Assert.All(result.Data, a => Assert.Contains("SIMULATED", a.DisplayName));
        Assert.False(service.Connection.IsConnected);
        Assert.False(service.Adapter.IsConnected);
    }

    [Fact]
    public async Task No_devices_is_success_with_empty_list()
    {
        await using var manager = new VciConnectionManager(new MockVciAdapter([]));

        var result = await manager.DiscoverAsync(None);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task Connect_and_disconnect_change_state()
    {
        await using var service = new MockDiagnosticService();

        var connect = await service.Connection.ConnectAsync("mock-sedan", None);

        Assert.Equal(OperationStatus.Success, connect.Status);
        Assert.True(connect.IsSimulated);
        Assert.Equal("mock-sedan", service.Connection.ConnectedAdapter?.AdapterId);

        var disconnect = await service.Connection.DisconnectAsync(None);

        Assert.Equal(OperationStatus.Success, disconnect.Status);
        Assert.False(service.Connection.IsConnected);
        Assert.False(service.Adapter.IsConnected);
    }

    [Fact]
    public async Task Unknown_and_busy_adapters_are_rejected()
    {
        await using var service = new MockDiagnosticService();

        var unknown = await service.Connection.ConnectAsync("missing", None);
        await service.Connection.ConnectAsync("mock-sedan", None);
        var busy = await service.Connection.ConnectAsync("mock-limited", None);
        var same = await service.Connection.ConnectAsync("mock-sedan", None);

        Assert.Equal("AdapterNotFound", unknown.ErrorCode);
        Assert.Equal("AdapterBusy", busy.ErrorCode);
        Assert.Equal(OperationStatus.Success, same.Status);
    }

    [Fact]
    public async Task Cancelled_discovery_is_reported()
    {
        await using var service = new MockDiagnosticService();

        var result = await service.Connection.DiscoverAsync(new CancellationToken(true));

        Assert.Equal(OperationStatus.Cancelled, result.Status);
    }

    [Fact]
    public async Task Hanging_connect_times_out_and_releases_adapter()
    {
        var adapter = new HangingAdapter { HangOnConnect = true };
        await using var manager = new VciConnectionManager(adapter, Fast);

        var result = await manager.ConnectAsync(HangingAdapter.Id, None);

        Assert.Equal(OperationStatus.Timeout, result.Status);
        Assert.Equal(1, adapter.DisconnectCount - 0);
        Assert.False(manager.IsConnected);
    }

    [Fact]
    public async Task Adapter_ignoring_its_timeout_is_cut_off_by_core()
    {
        await using var manager = new VciConnectionManager(new HangingAdapter(), Fast);
        var service = new DiagnosticService(manager);
        var session = await service.StartSessionAsync(HangingAdapter.Id, null, None);
        Assert.NotNull(session.Data);

        var result = await service.ReadVehicleIdentityAsync(session.Data.SessionId, None);

        Assert.Equal(OperationStatus.Timeout, result.Status);
        Assert.Null(result.Data);
    }

    private sealed class HangingAdapter : IVciAdapter
    {
        public const string Id = "hang";

        public bool HangOnConnect { get; init; }

        public int DisconnectCount { get; private set; }

        public bool IsConnected { get; private set; }

        public Task<IReadOnlyList<AdapterInfo>> DiscoverAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AdapterInfo>>(
                [new AdapterInfo(Id, "SIMULATED hanging adapter", "Test", "Test", "0", [])]);

        public Task<OperationResult<bool>> ConnectAsync(string adapterId, CancellationToken ct)
        {
            if (HangOnConnect)
            {
                return new TaskCompletionSource<OperationResult<bool>>().Task;
            }

            IsConnected = true;
            return Task.FromResult(Ok());
        }

        public Task<OperationResult<bool>> DisconnectAsync(CancellationToken ct)
        {
            DisconnectCount++;
            IsConnected = false;
            return Task.FromResult(Ok());
        }

        public Task<OperationResult<byte[]>> SendAsync(byte[] request, TimeSpan timeout, CancellationToken ct) =>
            new TaskCompletionSource<OperationResult<byte[]>>().Task;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static OperationResult<bool> Ok() =>
            new(OperationStatus.Success, true, null, null, DateTimeOffset.UtcNow, true);
    }
}

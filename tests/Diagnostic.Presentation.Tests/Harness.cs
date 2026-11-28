using Adapters.Abstractions;
using Adapters.Mock;
using Contracts;
using Diagnostic.Presentation.ViewModels;

namespace Diagnostic.Presentation.Tests;

/// <summary>
/// Runs the view models against the real engine (Diagnostic.Core) over the simulated adapter,
/// wired exactly as src/Diagnostic.Core/README.md prescribes: ManagedAdapter + the service.
/// Device 0 is the simulated sedan, device 1 the limited-support vehicle.
/// </summary>
internal sealed class Harness
{
    public MockDiagnosticService Mock { get; } = new();

    public IVciAdapter Adapter => Mock.ManagedAdapter;

    public IDiagnosticService Diagnostics => Mock;

    public AppSession Session { get; } = new();

    public ConnectionViewModel CreateConnection() => new(Adapter, Diagnostics, Session);

    public async Task ConnectAsync(int deviceIndex = 0)
    {
        var vm = CreateConnection();
        await vm.DiscoverCommand.ExecuteAsync();
        vm.SelectedDevice = vm.Devices[deviceIndex];
        await vm.ConnectCommand.ExecuteAsync();
        Assert.True(Session.IsActive, $"Connect failed: {vm.StatusKey} {vm.StatusDetail}");
    }

    public async Task ConnectAndScanAsync(int deviceIndex = 0)
    {
        await ConnectAsync(deviceIndex);
        await new ScanViewModel(Diagnostics, Session).RunCommand.ExecuteAsync();
    }

    public static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Condition was not met within 10 seconds.");
            await Task.Delay(10);
        }
    }
}

/// <summary>Adapter whose connection always times out, to test failure handling.</summary>
internal sealed class TimeoutAdapter : IVciAdapter
{
    private static readonly AdapterInfo Device =
        new("timeout-1", "Timeout device", "test", "test", "1", Array.Empty<string>());

    public bool IsConnected => false;

    public Task<IReadOnlyList<AdapterInfo>> DiscoverAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<AdapterInfo>>(new[] { Device });

    public Task<OperationResult<bool>> ConnectAsync(string adapterId, CancellationToken ct)
        => Task.FromResult(new OperationResult<bool>(
            OperationStatus.Timeout, false, "no answer", "TEST_TIMEOUT", DateTimeOffset.UtcNow, false));

    public Task<OperationResult<bool>> DisconnectAsync(CancellationToken ct)
        => Task.FromResult(new OperationResult<bool>(
            OperationStatus.Success, true, null, null, DateTimeOffset.UtcNow, false));

    public Task<OperationResult<byte[]>> SendAsync(byte[] request, TimeSpan timeout, CancellationToken ct)
        => Task.FromResult(new OperationResult<byte[]>(
            OperationStatus.Unsupported, null, null, null, DateTimeOffset.UtcNow, false));

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

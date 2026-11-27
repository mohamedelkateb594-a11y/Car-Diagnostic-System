using Adapters.Abstractions;
using Contracts;
using Diagnostic.Desktop.Mock;
using Diagnostic.Desktop.Services;
using Diagnostic.Desktop.ViewModels;

namespace Desktop.Tests;

/// <summary>Wires the UI mock services the same way App.xaml.cs does.</summary>
internal sealed class Harness
{
    public Harness()
    {
        Diagnostics = new UiMockDiagnosticService(Adapter);
    }

    public UiMockVciAdapter Adapter { get; } = new();

    public UiMockDiagnosticService Diagnostics { get; }

    public AppSession Session { get; } = new();

    public async Task ConnectAsync()
    {
        var vm = new ConnectionViewModel(Adapter, Diagnostics, Session);
        await vm.DiscoverCommand.ExecuteAsync();
        await vm.ConnectCommand.ExecuteAsync();
    }

    public async Task ConnectAndScanAsync()
    {
        await ConnectAsync();
        var scan = new ScanViewModel(Diagnostics, Session);
        await scan.RunCommand.ExecuteAsync();
    }

    public static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Condition was not met within 5 seconds.");
            await Task.Delay(10);
        }
    }
}

/// <summary>Adapter whose connection always times out, to test failure handling.</summary>
internal sealed class TimeoutAdapter : IVciAdapter
{
    private static readonly AdapterInfo Device = new("timeout-1", "Timeout device", "test", "test", "1", Array.Empty<string>());

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

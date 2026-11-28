using Adapters.Abstractions;
using Adapters.IsoTp;
using Adapters.Mock;
using Contracts;
using Diagnostic.Protocols;

namespace Diagnostic.Core.Tests;

/// <summary>
/// A desktop UI may block its thread while closing the engine, for example in an application exit
/// handler. These tests close the engine under a synchronization context that never runs posted work,
/// like a blocked dispatcher, and check that shutdown still completes.
/// </summary>
public class BlockedUiThreadTests
{
    private const string Sedan = "mock-sedan";
    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(5);
    private static readonly CancellationToken None = CancellationToken.None;

    [Fact]
    public async Task Shutdown_completes_when_the_adapter_disconnects_later()
    {
        var manager = new VciConnectionManager(new DelayedAdapter(new MockVciAdapter()));
        var managed = new ManagedVciAdapter(manager);
        Assert.Equal(OperationStatus.Success, (await managed.ConnectAsync(Sedan, None)).Status);

        Assert.True(CompletesOnBlockedThread(() => managed.DisposeAsync().AsTask()));
        Assert.False(managed.IsConnected);
    }

    [Fact]
    public async Task Shutdown_completes_while_a_read_is_in_flight()
    {
        // A long request timeout, so that the adapter guard cannot hide a stuck request.
        var options = new DiagnosticOptions { RequestTimeout = TimeSpan.FromSeconds(30) };
        var manager = new VciConnectionManager(new DelayedAdapter(new MockVciAdapter()), options);
        var service = new DiagnosticService(manager);
        var managed = new ManagedVciAdapter(manager);
        var started = await service.StartSessionAsync(Sedan, null, None);
        Assert.NotNull(started.Data);
        var sessionId = started.Data.SessionId;

        Assert.True(CompletesOnBlockedThread(() =>
        {
            // Started by the UI and still running when the application closes.
            _ = service.ReadVehicleIdentityAsync(sessionId, None);
            return managed.DisposeAsync().AsTask();
        }));
        Assert.False(managed.IsConnected);
    }

    [Fact]
    public async Task Shutdown_completes_when_the_can_channel_closes_later()
    {
        var info = new AdapterInfo("slow-can", "SIMULATED slow channel", "test", "test", "1", []);

        // Placeholder identifiers: no frame is sent in this test.
        var adapter = new IsoTpVciAdapter(
            info, () => new SlowClosingChannel(), new IsoTpOptions(1, 2, false), isSimulated: true);
        var manager = new VciConnectionManager(adapter);
        Assert.Equal(OperationStatus.Success, (await manager.ConnectAsync(info.AdapterId, None)).Status);

        Assert.True(CompletesOnBlockedThread(() => manager.DisposeAsync().AsTask()));
    }

    private static bool CompletesOnBlockedThread(Func<Task> shutdown)
    {
        var completed = false;
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new NeverRunningContext());
            var task = shutdown();
            completed = Task.WaitAny(new[] { task }, Limit) == 0 && task.IsCompletedSuccessfully;
        })
        {
            IsBackground = true
        };
        thread.Start();
        thread.Join();
        return completed;
    }

    private sealed class NeverRunningContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state)
        {
            // The thread that would run this is blocked.
        }
    }

    /// <summary>Answers like the wrapped adapter, but later, as real hardware would.</summary>
    private sealed class DelayedAdapter(IVciAdapter inner) : IVciAdapter
    {
        public bool IsConnected => inner.IsConnected;

        public Task<IReadOnlyList<AdapterInfo>> DiscoverAsync(CancellationToken ct) => inner.DiscoverAsync(ct);

        public Task<OperationResult<bool>> ConnectAsync(string adapterId, CancellationToken ct) =>
            inner.ConnectAsync(adapterId, ct);

        public Task<OperationResult<bool>> DisconnectAsync(CancellationToken ct) =>
            Later(() => inner.DisconnectAsync(ct));

        public Task<OperationResult<byte[]>> SendAsync(byte[] request, TimeSpan timeout, CancellationToken ct) =>
            Later(() => inner.SendAsync(request, timeout, ct));

        public ValueTask DisposeAsync() => inner.DisposeAsync();

        private static async Task<T> Later<T>(Func<Task<T>> call)
        {
            await Task.Delay(Delay).ConfigureAwait(false);
            return await call().ConfigureAwait(false);
        }
    }

    private sealed class SlowClosingChannel : ICanChannel, IAsyncDisposable
    {
        public Task SendAsync(CanFrame frame, CancellationToken ct) => Task.CompletedTask;

        public Task<CanFrame> ReceiveAsync(CancellationToken ct) =>
            Task.FromException<CanFrame>(new NotSupportedException());

        public Task DiscardReceivedAsync(CancellationToken ct) => Task.CompletedTask;

        public async ValueTask DisposeAsync() => await Task.Delay(Delay).ConfigureAwait(false);
    }
}

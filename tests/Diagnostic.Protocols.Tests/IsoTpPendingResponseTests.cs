using System.Threading.Channels;
using Contracts;

namespace Diagnostic.Protocols.Tests;

/// <summary>Runs against an in-memory channel only. CAN identifiers are arbitrary test values.</summary>
public class IsoTpPendingResponseTests
{
    private static readonly TimeSpan Long = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Short = TimeSpan.FromMilliseconds(100);
    private static readonly IsoTpOptions Options = new(0x100, 0x200, false);

    [Fact]
    public async Task Final_response_after_pending_replies_is_returned()
    {
        var channel = new ScriptedChannel(
            [0x03, 0x7F, 0x22, 0x78],
            [0x03, 0x7F, 0x22, 0x78],
            [0x04, 0x62, 0x12, 0x34, 0x56]);
        var transport = new IsoTpTransport(channel, Options);

        var result = await transport.ExchangeAsync([0x22, 0x12, 0x34], Long, CancellationToken.None);

        Assert.NotNull(result.Value);
        Assert.Equal("62123456", Convert.ToHexString(result.Value));
        Assert.Equal(1, channel.SendCount);
    }

    [Fact]
    public async Task Final_rejection_after_pending_is_returned_unchanged()
    {
        var channel = new ScriptedChannel([0x03, 0x7F, 0x22, 0x78], [0x03, 0x7F, 0x22, 0x31]);
        var transport = new IsoTpTransport(channel, Options);

        var result = await transport.ExchangeAsync([0x22, 0x12, 0x34], Long, CancellationToken.None);

        Assert.NotNull(result.Value);
        Assert.Equal("7F2231", Convert.ToHexString(result.Value));
    }

    [Fact]
    public async Task Pending_without_final_response_times_out_and_keeps_the_reply()
    {
        var channel = new ScriptedChannel([0x03, 0x7F, 0x22, 0x78]);
        var transport = new IsoTpTransport(channel, Options);

        var result = await transport.ExchangeAsync([0x22, 0x12, 0x34], Short, CancellationToken.None);

        Assert.Equal(OperationStatus.Timeout, result.Status);
        Assert.Equal("ResponsePendingTimeout", result.ErrorCode);
        Assert.Contains("7F 22 78", result.Message);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task Cancellation_while_pending_is_reported()
    {
        var channel = new ScriptedChannel([0x03, 0x7F, 0x22, 0x78]);
        var transport = new IsoTpTransport(channel, Options);
        using var cts = new CancellationTokenSource(Short);

        var result = await transport.ExchangeAsync([0x22, 0x12, 0x34], Long, cts.Token);

        Assert.Equal(OperationStatus.Cancelled, result.Status);
    }

    [Fact]
    public async Task Pending_reply_for_another_service_is_not_waited_on()
    {
        var channel = new ScriptedChannel([0x03, 0x7F, 0x19, 0x78]);
        var transport = new IsoTpTransport(channel, Options);

        var result = await transport.ExchangeAsync([0x22, 0x12, 0x34], Long, CancellationToken.None);

        Assert.NotNull(result.Value);
        Assert.Equal("7F1978", Convert.ToHexString(result.Value));
    }

    /// <summary>Queues all scripted frames when the request is sent.</summary>
    private sealed class ScriptedChannel(params byte[][] responses) : ICanChannel
    {
        private readonly Channel<CanFrame> _incoming = Channel.CreateUnbounded<CanFrame>();

        public int SendCount { get; private set; }

        public Task SendAsync(CanFrame frame, CancellationToken ct)
        {
            SendCount++;
            foreach (var data in responses)
            {
                _incoming.Writer.TryWrite(new CanFrame(0x200, false, data));
            }

            return Task.CompletedTask;
        }

        public async Task<CanFrame> ReceiveAsync(CancellationToken ct) => await _incoming.Reader.ReadAsync(ct);
    }
}

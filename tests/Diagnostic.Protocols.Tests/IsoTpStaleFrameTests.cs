using System.Threading.Channels;

namespace Diagnostic.Protocols.Tests;

/// <summary>Runs against an in-memory channel only. CAN identifiers are arbitrary test values.</summary>
public class IsoTpStaleFrameTests
{
    [Fact]
    public async Task Late_reply_is_not_read_as_the_answer_to_the_next_request()
    {
        var channel = new LateChannel();
        var transport = new IsoTpTransport(channel, new IsoTpOptions(0x100, 0x200, false));

        var first = await transport.ExchangeAsync(
            [0x01, 0x0C], TimeSpan.FromMilliseconds(100), CancellationToken.None);
        channel.Enqueue([0x04, 0x41, 0x0C, 0x11, 0x11]);
        channel.Reply = [0x04, 0x41, 0x0C, 0x22, 0x22];
        var second = await transport.ExchangeAsync(
            [0x01, 0x0C], TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.Null(first.Value);
        Assert.NotNull(second.Value);
        Assert.Equal("410C2222", Convert.ToHexString(second.Value));
    }

    private sealed class LateChannel : ICanChannel
    {
        private readonly Channel<CanFrame> _incoming = Channel.CreateUnbounded<CanFrame>();

        public byte[]? Reply { get; set; }

        public void Enqueue(byte[] data) => _incoming.Writer.TryWrite(new CanFrame(0x200, false, data));

        public Task SendAsync(CanFrame frame, CancellationToken ct)
        {
            if (Reply is not null)
            {
                Enqueue(Reply);
            }

            return Task.CompletedTask;
        }

        public async Task<CanFrame> ReceiveAsync(CancellationToken ct) => await _incoming.Reader.ReadAsync(ct);

        public Task DiscardReceivedAsync(CancellationToken ct)
        {
            while (_incoming.Reader.TryRead(out _))
            {
            }

            return Task.CompletedTask;
        }
    }
}

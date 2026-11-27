using System.Threading.Channels;
using Contracts;

namespace Diagnostic.Protocols.Tests;

/// <summary>Runs against an in-memory channel only. CAN identifiers are arbitrary test values.</summary>
public class IsoTpTransportTests
{
    private const uint RequestId = 0x100;
    private const uint ResponseId = 0x200;
    private static readonly TimeSpan Long = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Short = TimeSpan.FromMilliseconds(100);
    private static readonly IsoTpOptions Options = new(RequestId, ResponseId, false);

    private static byte[] Sequence(int length) => Enumerable.Range(0, length).Select(i => (byte)i).ToArray();

    private static List<byte[]> Segment(byte[] payload)
    {
        var frames = new List<byte[]>
        {
            new[] { (byte)(0x10 | (payload.Length >> 8)), (byte)payload.Length }.Concat(payload.Take(6)).ToArray(),
        };
        var sequence = 1;
        for (var offset = 6; offset < payload.Length; offset += 7)
        {
            frames.Add(new[] { (byte)(0x20 | (sequence++ & 0x0F)) }.Concat(payload.Skip(offset).Take(7)).ToArray());
        }

        return frames;
    }

    [Fact]
    public async Task Single_frames_are_exchanged()
    {
        var channel = new FakeCanChannel();
        channel.OnSend = _ => channel.Enqueue([0x03, 0x41, 0x0D, 0x50]);
        var transport = new IsoTpTransport(channel, Options);

        var result = await transport.ExchangeAsync([0x01, 0x0D], Long, CancellationToken.None);

        Assert.NotNull(result.Value);
        Assert.Equal("410D50", Convert.ToHexString(result.Value));
        Assert.Equal("02010D", Convert.ToHexString(Assert.Single(channel.Sent)));
    }

    [Fact]
    public async Task Frames_are_padded_when_configured()
    {
        var channel = new FakeCanChannel();
        channel.OnSend = _ => channel.Enqueue([0x01, 0x43]);
        var transport = new IsoTpTransport(channel, Options with { PaddingByte = 0xAA });

        await transport.ExchangeAsync([0x03], Long, CancellationToken.None);

        Assert.Equal("0103AAAAAAAAAAAA", Convert.ToHexString(Assert.Single(channel.Sent)));
    }

    [Theory]
    [InlineData(8)]
    [InlineData(20)]
    [InlineData(200)]
    public async Task Segmented_response_is_reassembled(int length)
    {
        var payload = Sequence(length);
        var frames = Segment(payload);
        var channel = new FakeCanChannel();
        channel.OnSend = sent =>
        {
            if (sent[0] == 0x30)
            {
                frames.Skip(1).ToList().ForEach(f => channel.Enqueue(f));
            }
            else
            {
                channel.Enqueue(frames[0]);
            }
        };
        var transport = new IsoTpTransport(channel, Options);

        var result = await transport.ExchangeAsync([0x09, 0x02], Long, CancellationToken.None);

        Assert.NotNull(result.Value);
        Assert.Equal(Convert.ToHexString(payload), Convert.ToHexString(result.Value));
    }

    [Fact]
    public async Task Flow_control_is_repeated_per_receive_block()
    {
        var frames = Segment(Sequence(20));
        var channel = new FakeCanChannel();
        var next = 0;
        channel.OnSend = _ => channel.Enqueue(frames[next++]);
        var transport = new IsoTpTransport(channel, Options with { ReceiveBlockSize = 1 });

        var result = await transport.ExchangeAsync([0x09, 0x02], Long, CancellationToken.None);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(2, channel.Sent.Count(f => f[0] == 0x30));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public async Task Segmented_request_follows_flow_control(int blockSize)
    {
        var payload = Sequence(125);
        var expected = Segment(payload);
        var channel = new FakeCanChannel();
        var consecutive = 0;
        channel.OnSend = sent =>
        {
            if (sent[0] >> 4 == 1)
            {
                channel.Enqueue([0x30, (byte)blockSize, 0x00]);
                return;
            }

            consecutive++;
            if (consecutive == expected.Count - 1)
            {
                channel.Enqueue([0x01, 0x62]);
            }
            else if (blockSize > 0 && consecutive % blockSize == 0)
            {
                channel.Enqueue([0x30, (byte)blockSize, 0x00]);
            }
        };
        var transport = new IsoTpTransport(channel, Options);

        var result = await transport.ExchangeAsync(payload, Long, CancellationToken.None);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(
            string.Join(',', expected.Select(Convert.ToHexString)),
            string.Join(',', channel.Sent.Select(Convert.ToHexString)));
    }

    [Fact]
    public async Task Wait_frame_is_followed_by_continue()
    {
        var channel = new FakeCanChannel();
        channel.OnSend = sent =>
        {
            if (sent[0] >> 4 == 1)
            {
                channel.Enqueue([0x31, 0x00, 0x00]);
                channel.Enqueue([0x30, 0x00, 0x00]);
            }
            else
            {
                channel.Enqueue([0x01, 0x62]);
            }
        };
        var transport = new IsoTpTransport(channel, Options);

        var result = await transport.ExchangeAsync(Sequence(10), Long, CancellationToken.None);

        Assert.Equal(OperationStatus.Success, result.Status);
    }

    [Fact]
    public async Task Overflow_is_a_transport_error()
    {
        var channel = new FakeCanChannel();
        channel.OnSend = _ => channel.Enqueue([0x32, 0x00, 0x00]);
        var transport = new IsoTpTransport(channel, Options);

        var result = await transport.ExchangeAsync(Sequence(10), Long, CancellationToken.None);

        Assert.Equal(OperationStatus.TransportError, result.Status);
    }

    [Fact]
    public async Task Wrong_sequence_number_is_a_protocol_error()
    {
        var frames = Segment(Sequence(20));
        var channel = new FakeCanChannel();
        channel.OnSend = sent => channel.Enqueue(sent[0] == 0x30 ? frames[2] : frames[0]);
        var transport = new IsoTpTransport(channel, Options);

        var result = await transport.ExchangeAsync([0x09, 0x02], Long, CancellationToken.None);

        Assert.Equal(OperationStatus.ProtocolError, result.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task Missing_response_times_out()
    {
        var transport = new IsoTpTransport(new FakeCanChannel(), Options);

        var result = await transport.ExchangeAsync([0x03], Short, CancellationToken.None);

        Assert.Equal(OperationStatus.Timeout, result.Status);
    }

    [Fact]
    public async Task Missing_flow_control_times_out()
    {
        var transport = new IsoTpTransport(new FakeCanChannel(), Options with { FlowControlTimeout = Short });

        var result = await transport.ExchangeAsync(Sequence(10), Long, CancellationToken.None);

        Assert.Equal(OperationStatus.Timeout, result.Status);
    }

    [Fact]
    public async Task Cancellation_is_reported()
    {
        var transport = new IsoTpTransport(new FakeCanChannel(), Options);
        using var cts = new CancellationTokenSource(Short);

        var result = await transport.ExchangeAsync([0x03], Long, cts.Token);

        Assert.Equal(OperationStatus.Cancelled, result.Status);
    }

    [Fact]
    public async Task Frames_from_other_identifiers_are_ignored()
    {
        var channel = new FakeCanChannel();
        channel.OnSend = _ =>
        {
            channel.Enqueue([0x01, 0x7F], 0x300);
            channel.Enqueue([0x01, 0x43]);
        };
        var transport = new IsoTpTransport(channel, Options);

        var result = await transport.ExchangeAsync([0x03], Long, CancellationToken.None);

        Assert.NotNull(result.Value);
        Assert.Equal("43", Convert.ToHexString(result.Value));
    }

    [Fact]
    public async Task Oversized_request_is_not_sent()
    {
        var channel = new FakeCanChannel();
        var transport = new IsoTpTransport(channel, Options);

        var result = await transport.ExchangeAsync(new byte[4096], Long, CancellationToken.None);

        Assert.Equal(OperationStatus.Failed, result.Status);
        Assert.Empty(channel.Sent);
    }

    [Fact]
    public async Task Channel_failure_is_a_transport_error()
    {
        var channel = new FakeCanChannel { OnSend = _ => throw new IOException("Simulated bus failure.") };
        var transport = new IsoTpTransport(channel, Options);

        var result = await transport.ExchangeAsync([0x03], Long, CancellationToken.None);

        Assert.Equal(OperationStatus.TransportError, result.Status);
    }

    [Fact]
    public void Out_of_range_identifier_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new IsoTpTransport(new FakeCanChannel(), new IsoTpOptions(0x800, ResponseId, false)));
    }

    private sealed class FakeCanChannel : ICanChannel
    {
        private readonly Channel<CanFrame> _incoming = Channel.CreateUnbounded<CanFrame>();

        public List<byte[]> Sent { get; } = [];

        public Action<byte[]>? OnSend { get; set; }

        public void Enqueue(byte[] data, uint id = ResponseId) =>
            _incoming.Writer.TryWrite(new CanFrame(id, false, data));

        public Task SendAsync(CanFrame frame, CancellationToken ct)
        {
            Sent.Add(frame.Data);
            OnSend?.Invoke(frame.Data);
            return Task.CompletedTask;
        }

        public async Task<CanFrame> ReceiveAsync(CancellationToken ct) => await _incoming.Reader.ReadAsync(ct);
    }
}

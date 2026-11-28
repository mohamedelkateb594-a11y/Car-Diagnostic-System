using System.Threading.Channels;
using Diagnostic.Protocols;

namespace Adapters.Mock;

/// <summary>
/// SIMULATED ECU behind an in-memory CAN channel. It speaks the ECU side of ISO-TP for one
/// request at a time. It ignores the tester's block size and sends all remaining frames.
/// </summary>
public sealed class SimulatedCanEcu : ICanChannel
{
    private readonly uint _requestId;
    private readonly uint _responseId;
    private readonly Func<byte[], byte[]?> _responder;
    private readonly Channel<CanFrame> _outgoing = Channel.CreateUnbounded<CanFrame>();
    private readonly Queue<byte[]> _pending = new();
    private byte[]? _request;
    private int _received;
    private int _sequence;

    /// <param name="responder">Maps a service request to a response; null means the ECU stays silent.</param>
    public SimulatedCanEcu(uint requestId, uint responseId, Func<byte[], byte[]?> responder)
    {
        ArgumentNullException.ThrowIfNull(responder);
        _requestId = requestId;
        _responseId = responseId;
        _responder = responder;
    }

    public Task SendAsync(CanFrame frame, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (ct.IsCancellationRequested)
        {
            return Task.FromCanceled(ct);
        }

        if (frame.Id == _requestId && frame.Data.Length > 0)
        {
            Handle(frame.Data);
        }

        return Task.CompletedTask;
    }

    public async Task<CanFrame> ReceiveAsync(CancellationToken ct) => await _outgoing.Reader.ReadAsync(ct);

    public Task DiscardReceivedAsync(CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            return Task.FromCanceled(ct);
        }

        while (_outgoing.Reader.TryRead(out _))
        {
        }

        return Task.CompletedTask;
    }

    private void Handle(byte[] data)
    {
        switch (data[0] >> 4)
        {
            case 0:
                var length = data[0] & 0x0F;
                if (length > 0 && data.Length > length)
                {
                    Respond(data[1..(length + 1)]);
                }

                break;
            case 1 when data.Length == 8:
                var total = ((data[0] & 0x0F) << 8) | data[1];
                if (total >= 8)
                {
                    _request = new byte[total];
                    Array.Copy(data, 2, _request, 0, 6);
                    _received = 6;
                    _sequence = 1;
                    Enqueue([0x30, 0x00, 0x00]);
                }

                break;
            case 2 when _request is not null && (data[0] & 0x0F) == _sequence:
                var count = Math.Min(data.Length - 1, _request.Length - _received);
                Array.Copy(data, 1, _request, _received, count);
                _received += count;
                _sequence = (_sequence + 1) & 0x0F;
                if (_received == _request.Length)
                {
                    var complete = _request;
                    _request = null;
                    Respond(complete);
                }

                break;
            case 3 when data[0] == 0x30:
                while (_pending.TryDequeue(out var next))
                {
                    Enqueue(next);
                }

                break;
        }
    }

    private void Respond(byte[] request)
    {
        _pending.Clear();
        var response = _responder(request);
        if (response is null || response.Length is 0 or > IsoTpTransport.MaxPayloadLength)
        {
            return;
        }

        if (response.Length < 8)
        {
            Enqueue([(byte)response.Length, .. response]);
            return;
        }

        Enqueue([(byte)(0x10 | (response.Length >> 8)), (byte)(response.Length & 0xFF), .. response[..6]]);
        byte sequence = 1;
        for (var offset = 6; offset < response.Length; offset += 7)
        {
            var end = Math.Min(offset + 7, response.Length);
            _pending.Enqueue([(byte)(0x20 | sequence), .. response[offset..end]]);
            sequence = (byte)((sequence + 1) & 0x0F);
        }
    }

    private void Enqueue(byte[] data) => _outgoing.Writer.TryWrite(new CanFrame(_responseId, false, data));
}

// ISO-TP (ISO 15765-2) subset: classical CAN, normal addressing, payloads up to 4095 bytes.
// Sources: ../SOURCES.md. Not supported: CAN FD, extended/mixed addressing, lengths above 4095,
// and sending Wait or Overflow flow-control frames. Not tested on a real bus.
using Contracts;

namespace Diagnostic.Protocols;

/// <summary>CAN identifiers are always supplied by the caller; none are built in.</summary>
public sealed record IsoTpOptions(uint RequestId, uint ResponseId, bool IsExtendedId)
{
    public byte? PaddingByte { get; init; }

    // Temporary defaults; adjust per vehicle definition.
    public TimeSpan FlowControlTimeout { get; init; } = TimeSpan.FromSeconds(1);

    public TimeSpan ConsecutiveFrameTimeout { get; init; } = TimeSpan.FromSeconds(1);

    public int MaxWaitFrames { get; init; } = 10;

    /// <summary>Block size advertised in our flow control. Zero means no further flow control.</summary>
    public byte ReceiveBlockSize { get; init; }

    /// <summary>Raw STmin byte advertised in our flow control.</summary>
    public byte ReceiveSeparationTime { get; init; }
}

public sealed class IsoTpTransport
{
    public const int MaxPayloadLength = 4095;
    private const int FrameLength = 8;

    private readonly ICanChannel _channel;
    private readonly IsoTpOptions _options;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public IsoTpTransport(ICanChannel channel, IsoTpOptions options, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(options);

        var maxId = options.IsExtendedId ? 0x1FFFFFFFu : 0x7FFu;
        if (options.RequestId > maxId || options.ResponseId > maxId)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "A CAN identifier is out of range.");
        }

        _channel = channel;
        _options = options;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Sends one service payload and returns the reassembled response payload. A "response pending"
    /// reply (7F, service, 78) is followed by waiting for the final response within the same timeout.
    /// </summary>
    public async Task<ParseResult<byte[]>> ExchangeAsync(byte[] request, TimeSpan timeout, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Length is 0 or > MaxPayloadLength || timeout <= TimeSpan.Zero)
        {
            return Fail(OperationStatus.Failed, "InvalidRequest", "Invalid payload length or timeout.");
        }

        byte[]? pending = null;
        using var deadline = new CancellationTokenSource(timeout, _time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        try
        {
            await _gate.WaitAsync(linked.Token);
            try
            {
                var failure = await SendMessageAsync(request, linked.Token);
                if (failure is not null)
                {
                    return failure;
                }

                while (true)
                {
                    var response = await ReceiveMessageAsync(linked.Token);
                    if (response.Value is not [0x7F, var service, 0x78] || service != request[0])
                    {
                        return response;
                    }

                    pending = response.Value;
                }
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return Fail(OperationStatus.Cancelled, "Cancelled", "The exchange was cancelled.");
        }
        catch (OperationCanceledException) when (pending is not null)
        {
            // The original reply is kept in the message.
            return Fail(
                OperationStatus.Timeout,
                "ResponsePendingTimeout",
                $"No final response after {ServiceResponse.Hex(pending)}.");
        }
        catch (OperationCanceledException)
        {
            return Fail(OperationStatus.Timeout, "IsoTpTimeout", "Timed out waiting for the CAN channel.");
        }
        catch (Exception ex)
        {
            return Fail(OperationStatus.TransportError, "CanChannelError", $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private async Task<ParseResult<byte[]>?> SendMessageAsync(byte[] payload, CancellationToken ct)
    {
        if (payload.Length < FrameLength)
        {
            await SendFrameAsync([(byte)payload.Length, .. payload], ct);
            return null;
        }

        await SendFrameAsync(
            [(byte)(0x10 | (payload.Length >> 8)), (byte)(payload.Length & 0xFF), .. payload[..6]], ct);

        var offset = 6;
        byte sequence = 1;
        var waitFrames = 0;
        while (offset < payload.Length)
        {
            var control = await ReceiveFrameAsync(_options.FlowControlTimeout, ct);
            if (control.Length == 0 || control[0] >> 4 != 3)
            {
                continue;
            }

            if (control.Length < 3)
            {
                return Violation("The flow control frame is too short.");
            }

            switch (control[0] & 0x0F)
            {
                case 0:
                    break;
                case 1:
                    if (++waitFrames > _options.MaxWaitFrames)
                    {
                        return Fail(OperationStatus.Timeout, "IsoTpWaitLimit", "Too many wait frames.");
                    }

                    continue;
                case 2:
                    return Fail(OperationStatus.TransportError, "IsoTpOverflow", "The receiver reported overflow.");
                default:
                    return Violation("Unknown flow status.");
            }

            waitFrames = 0;
            int blockSize = control[1];
            var separation = DecodeSeparationTime(control[2]);
            var sentInBlock = 0;
            while (offset < payload.Length && (blockSize == 0 || sentInBlock < blockSize))
            {
                if (sentInBlock > 0 && separation > TimeSpan.Zero)
                {
                    await Task.Delay(separation, _time, ct);
                }

                var count = Math.Min(7, payload.Length - offset);
                await SendFrameAsync([(byte)(0x20 | sequence), .. payload[offset..(offset + count)]], ct);
                offset += count;
                sequence = (byte)((sequence + 1) & 0x0F);
                sentInBlock++;
            }
        }

        return null;
    }

    private async Task<ParseResult<byte[]>> ReceiveMessageAsync(CancellationToken ct)
    {
        while (true)
        {
            var frame = await ReceiveFrameAsync(ct);
            if (frame.Length == 0)
            {
                return Violation("Empty CAN frame.");
            }

            switch (frame[0] >> 4)
            {
                case 0:
                    var length = frame[0] & 0x0F;
                    if (length is 0 or > 7 || frame.Length < length + 1)
                    {
                        return Violation("Invalid single frame length.");
                    }

                    return ParseResult<byte[]>.Ok(frame[1..(length + 1)]);
                case 1:
                    return await ReceiveSegmentedAsync(frame, ct);
                default:
                    // Stray consecutive or flow control frames before a message are ignored.
                    break;
            }
        }
    }

    private async Task<ParseResult<byte[]>> ReceiveSegmentedAsync(byte[] first, CancellationToken ct)
    {
        if (first.Length != FrameLength)
        {
            return Violation("The first frame must hold 8 bytes.");
        }

        var length = ((first[0] & 0x0F) << 8) | first[1];
        if (length < FrameLength)
        {
            return Violation("Unsupported or invalid first frame length.");
        }

        var payload = new byte[length];
        Array.Copy(first, 2, payload, 0, 6);
        var received = 6;
        byte expected = 1;
        var framesInBlock = 0;
        await SendFlowControlAsync(ct);

        while (received < length)
        {
            var frame = await ReceiveFrameAsync(_options.ConsecutiveFrameTimeout, ct);
            if (frame.Length == 0)
            {
                return Violation("Empty CAN frame.");
            }

            var type = frame[0] >> 4;
            if (type == 3)
            {
                continue;
            }

            if (type != 2 || (frame[0] & 0x0F) != expected)
            {
                return Violation("Unexpected frame type or sequence number.");
            }

            var count = Math.Min(7, length - received);
            if (frame.Length < count + 1)
            {
                return Violation("The consecutive frame is too short.");
            }

            Array.Copy(frame, 1, payload, received, count);
            received += count;
            expected = (byte)((expected + 1) & 0x0F);

            if (_options.ReceiveBlockSize > 0
                && ++framesInBlock == _options.ReceiveBlockSize
                && received < length)
            {
                framesInBlock = 0;
                await SendFlowControlAsync(ct);
            }
        }

        return ParseResult<byte[]>.Ok(payload);
    }

    private Task SendFlowControlAsync(CancellationToken ct) =>
        SendFrameAsync([0x30, _options.ReceiveBlockSize, _options.ReceiveSeparationTime], ct);

    private Task SendFrameAsync(byte[] data, CancellationToken ct)
    {
        if (_options.PaddingByte is byte padding && data.Length < FrameLength)
        {
            var padded = new byte[FrameLength];
            Array.Fill(padded, padding);
            data.CopyTo(padded, 0);
            data = padded;
        }

        var frame = new CanFrame(_options.RequestId, _options.IsExtendedId, data);
        return _channel.SendAsync(frame, ct).WaitAsync(ct);
    }

    private async Task<byte[]> ReceiveFrameAsync(TimeSpan stepTimeout, CancellationToken ct)
    {
        using var timer = new CancellationTokenSource(stepTimeout, _time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timer.Token);
        return await ReceiveFrameAsync(linked.Token);
    }

    private async Task<byte[]> ReceiveFrameAsync(CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var frame = await _channel.ReceiveAsync(ct).WaitAsync(ct);
            if (frame.Id == _options.ResponseId && frame.IsExtended == _options.IsExtendedId)
            {
                return frame.Data;
            }
        }
    }

    // Sub-millisecond values are rounded up to 1 ms; reserved values use the 127 ms maximum.
    private static TimeSpan DecodeSeparationTime(byte value) => value switch
    {
        <= 0x7F => TimeSpan.FromMilliseconds((double)value),
        >= 0xF1 and <= 0xF9 => TimeSpan.FromMilliseconds(1.0),
        _ => TimeSpan.FromMilliseconds(127.0),
    };

    private static ParseResult<byte[]> Violation(string message) =>
        Fail(OperationStatus.ProtocolError, "IsoTpProtocolError", message);

    private static ParseResult<byte[]> Fail(OperationStatus status, string errorCode, string message) =>
        ParseResult<byte[]>.Fail(status, errorCode, message);
}

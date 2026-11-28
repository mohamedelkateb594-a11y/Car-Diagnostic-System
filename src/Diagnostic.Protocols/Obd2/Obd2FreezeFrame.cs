// Request "02 PID frame" and response "42 PID frame data": see ../SOURCES.md.
namespace Diagnostic.Protocols;

/// <summary>Read-only OBD-II freeze frame requests and parsers.</summary>
public static class Obd2FreezeFrame
{
    public const byte ServiceId = 0x02;
    public const byte TriggerDtcPid = 0x02;

    public static byte[] Request(byte pid, byte frame) => [ServiceId, pid, frame];

    /// <summary>The code that caused the frame. An empty string means that no frame is stored.</summary>
    public static ParseResult<string> ParseTriggerDtc(byte[] response, byte frame)
    {
        var failure = Check<string>(response, TriggerDtcPid, frame, 2);
        if (failure is not null)
        {
            return failure;
        }

        var none = response[3] == 0 && response[4] == 0;
        return ParseResult<string>.Ok(none ? string.Empty : Obd2Parser.DecodeDtc(response[3], response[4]));
    }

    public static ParseResult<double> ParseValue(byte[] response, byte frame, Obd2PidDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var failure = Check<double>(response, definition.Pid, frame, definition.Length);
        if (failure is not null)
        {
            return failure;
        }

        var second = definition.Length > 1 ? response[4] : (byte)0;
        return ParseResult<double>.Ok(definition.Decode(response[3], second));
    }

    private static ParseResult<T>? Check<T>(byte[] response, byte pid, byte frame, int dataLength)
    {
        var failure = ServiceResponse.CheckPositive<T>(response, ServiceId);
        if (failure is not null)
        {
            return failure;
        }

        return response.Length == 3 + dataLength && response[1] == pid && response[2] == frame
            ? null
            : ServiceResponse.Invalid<T>($"Invalid freeze frame response: {ServiceResponse.Hex(response)}");
    }
}

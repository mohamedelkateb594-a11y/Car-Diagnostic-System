using Contracts;

namespace Diagnostic.Protocols;

/// <summary>
/// Common checks for diagnostic service responses (payload level, without CAN or ISO-TP bytes).
/// Response code values and names: see SOURCES.md (udsoncan, MIT).
/// </summary>
public static class ServiceResponse
{
    public const byte NegativeResponseId = 0x7F;
    public const byte PositiveResponseOffset = 0x40;
    public const string NegativeResponseErrorCode = "NegativeResponse";
    public const string InvalidResponseErrorCode = "InvalidResponse";

    /// <summary>Returns null for a positive response to the service, otherwise the failure.</summary>
    public static ParseResult<T>? CheckPositive<T>(byte[] response, byte serviceId)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (response.Length == 0)
        {
            return Invalid<T>("The response is empty.");
        }

        if (response[0] == NegativeResponseId)
        {
            if (response.Length != 3 || response[1] != serviceId)
            {
                return Invalid<T>($"Malformed negative response: {Hex(response)}");
            }

            // Temporary decision (contract PR #1 is not approved yet): the original bytes are kept
            // in Message and ErrorCode is NegativeResponse. A rejection is never a success.
            return ParseResult<T>.Fail(MapResponseCode(response[2]), NegativeResponseErrorCode, Hex(response));
        }

        return response[0] == serviceId + PositiveResponseOffset
            ? null
            : Invalid<T>($"Unexpected response to service {serviceId:X2}: {Hex(response)}");
    }

    public static ParseResult<T> Invalid<T>(string message) =>
        ParseResult<T>.Fail(OperationStatus.InvalidResponse, InvalidResponseErrorCode, message);

    public static string Hex(byte[] bytes) => string.Join(' ', bytes.Select(b => b.ToString("X2")));

    private static OperationStatus MapResponseCode(byte code) => code switch
    {
        0x11 or 0x12 => OperationStatus.Unsupported,
        0x7E or 0x7F => OperationStatus.NotApplicable,
        _ => OperationStatus.Failed,
    };
}

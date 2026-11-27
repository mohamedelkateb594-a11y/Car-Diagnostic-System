namespace Diagnostic.Protocols;

/// <summary>Parsers for read-only OBD-II service responses. Sources and limits: ../SOURCES.md.</summary>
public static class Obd2Parser
{
    private const int VinLength = 17;

    /// <summary>Decodes the 32-bit support bitmap that follows PID 00, 20 or 40.</summary>
    public static ParseResult<IReadOnlySet<byte>> ParseSupportedPids(byte[] response, byte basePid)
    {
        var failure = ServiceResponse.CheckPositive<IReadOnlySet<byte>>(response, Obd2Service.CurrentData);
        if (failure is not null)
        {
            return failure;
        }

        if (response.Length != 6 || response[1] != basePid)
        {
            return ServiceResponse.Invalid<IReadOnlySet<byte>>(
                $"Invalid supported-PID response: {ServiceResponse.Hex(response)}");
        }

        var supported = new HashSet<byte>();
        for (var index = 0; index < 32; index++)
        {
            var isSet = (response[2 + (index / 8)] & (0x80 >> (index % 8))) != 0;
            var pid = basePid + index + 1;
            if (isSet && pid <= byte.MaxValue)
            {
                supported.Add((byte)pid);
            }
        }

        return ParseResult<IReadOnlySet<byte>>.Ok(supported);
    }

    public static ParseResult<double> ParseLiveValue(byte[] response, Obd2PidDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var failure = ServiceResponse.CheckPositive<double>(response, Obd2Service.CurrentData);
        if (failure is not null)
        {
            return failure;
        }

        if (response.Length != 2 + definition.Length || response[1] != definition.Pid)
        {
            return ServiceResponse.Invalid<double>(
                $"Invalid response for PID {definition.Pid:X2}: {ServiceResponse.Hex(response)}");
        }

        var second = definition.Length > 1 ? response[3] : (byte)0;
        return ParseResult<double>.Ok(definition.Decode(response[2], second));
    }

    public static ParseResult<string> ParseVin(byte[] response)
    {
        var failure = ServiceResponse.CheckPositive<string>(response, Obd2Service.VehicleInformation);
        if (failure is not null)
        {
            return failure;
        }

        if (response.Length < 2 || response[1] != Obd2Request.VinPid)
        {
            return ServiceResponse.Invalid<string>("The response is not a VIN response.");
        }

        // Temporary decision: the source gives 17 ASCII bytes, left padded with 00. A leading item
        // count of 01 (18 bytes) is also accepted; confirm the layout with real hardware.
        var start = response.Length == 2 + VinLength + 1 && response[2] == 0x01 ? 3 : 2;
        if (response.Length - start != VinLength)
        {
            return ServiceResponse.Invalid<string>("The VIN field does not hold 17 bytes.");
        }

        var characters = response.Skip(start).SkipWhile(b => b == 0).Select(b => (char)b).ToArray();
        if (characters.Length == 0 || !characters.All(char.IsAsciiLetterOrDigit))
        {
            // The bytes are not echoed here because a VIN is personal data.
            return ServiceResponse.Invalid<string>("The VIN field is empty or holds invalid characters.");
        }

        return ParseResult<string>.Ok(new string(characters));
    }

    public static ParseResult<IReadOnlyList<string>> ParseDtcs(byte[] response, byte serviceId)
    {
        var failure = ServiceResponse.CheckPositive<IReadOnlyList<string>>(response, serviceId);
        if (failure is not null)
        {
            return failure;
        }

        // Temporary decision: an odd body length means "count byte + pairs" (CAN style); an even
        // length means pairs only, with 0000 as padding. Confirm with real hardware.
        var bodyLength = response.Length - 1;
        var hasCount = bodyLength % 2 == 1;
        if (bodyLength == 0 || (hasCount && response[1] * 2 != bodyLength - 1))
        {
            return ServiceResponse.Invalid<IReadOnlyList<string>>(
                $"Invalid DTC response: {ServiceResponse.Hex(response)}");
        }

        var codes = new List<string>();
        for (var index = hasCount ? 2 : 1; index + 1 < response.Length; index += 2)
        {
            if (response[index] != 0 || response[index + 1] != 0)
            {
                codes.Add(DecodeDtc(response[index], response[index + 1]));
            }
        }

        return ParseResult<IReadOnlyList<string>>.Ok(codes);
    }

    public static string DecodeDtc(byte high, byte low) =>
        $"{"PCBU"[high >> 6]}{(high >> 4) & 0x03}{high & 0x0F:X1}{low:X2}";
}

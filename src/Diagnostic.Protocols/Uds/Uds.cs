// Read-only UDS foundations. Service and status-bit values: see ../SOURCES.md.
// Not implemented: sessions, security access, response-pending (0x78) waiting, and every
// state-changing service.
namespace Diagnostic.Protocols;

public static class UdsService
{
    public const byte ReadDtcInformation = 0x19;
    public const byte ReadDataByIdentifier = 0x22;
    public const byte ReportDtcByStatusMask = 0x02;
}

public static class UdsRequest
{
    /// <summary>The caller supplies the identifier from a verified vehicle definition.</summary>
    public static byte[] ReadDataByIdentifier(ushort identifier) =>
        [UdsService.ReadDataByIdentifier, (byte)(identifier >> 8), (byte)(identifier & 0xFF)];

    public static byte[] ReadDtcsByStatusMask(byte statusMask) =>
        [UdsService.ReadDtcInformation, UdsService.ReportDtcByStatusMask, statusMask];
}

public sealed record UdsDtc(string Code, byte StatusByte)
{
    public bool IsPending => (StatusByte & 0x04) != 0;

    public bool IsConfirmed => (StatusByte & 0x08) != 0;
}

public static class UdsParser
{
    public static ParseResult<byte[]> ParseDataByIdentifier(byte[] response, ushort identifier)
    {
        var failure = ServiceResponse.CheckPositive<byte[]>(response, UdsService.ReadDataByIdentifier);
        if (failure is not null)
        {
            return failure;
        }

        if (response.Length < 4 || ((response[1] << 8) | response[2]) != identifier)
        {
            return ServiceResponse.Invalid<byte[]>(
                $"The response does not hold data for identifier {identifier:X4}.");
        }

        return ParseResult<byte[]>.Ok(response[3..]);
    }

    /// <summary>Layout assumed: 59 02 availabilityMask, then 3 DTC bytes and 1 status byte per record.</summary>
    public static ParseResult<IReadOnlyList<UdsDtc>> ParseDtcsByStatusMask(byte[] response)
    {
        var failure = ServiceResponse.CheckPositive<IReadOnlyList<UdsDtc>>(response, UdsService.ReadDtcInformation);
        if (failure is not null)
        {
            return failure;
        }

        if (response.Length < 3
            || response[1] != UdsService.ReportDtcByStatusMask
            || (response.Length - 3) % 4 != 0)
        {
            return ServiceResponse.Invalid<IReadOnlyList<UdsDtc>>(
                $"Invalid DTC information response: {ServiceResponse.Hex(response)}");
        }

        var records = new List<UdsDtc>();
        for (var index = 3; index < response.Length; index += 4)
        {
            var code = $"{Obd2Parser.DecodeDtc(response[index], response[index + 1])}-{response[index + 2]:X2}";
            records.Add(new UdsDtc(code, response[index + 3]));
        }

        return ParseResult<IReadOnlyList<UdsDtc>>.Ok(records);
    }
}

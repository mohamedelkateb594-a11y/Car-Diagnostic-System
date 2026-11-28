using System.Text;
using Contracts;

namespace Diagnostic.Protocols.Tests;

/// <summary>All byte sequences are synthetic test data.</summary>
public class Obd2ParserTests
{
    private static byte[] Bytes(string hex) => Convert.FromHexString(hex);

    [Theory]
    [InlineData("obd2.01.04", "4104FF", 100.0)]
    [InlineData("obd2.01.05", "41057B", 83.0)]
    [InlineData("obd2.01.06", "410680", 0.0)]
    [InlineData("obd2.01.0A", "410A64", 300.0)]
    [InlineData("obd2.01.0B", "410B23", 35.0)]
    [InlineData("obd2.01.0C", "410C0C80", 800.0)]
    [InlineData("obd2.01.0D", "410D50", 80.0)]
    [InlineData("obd2.01.0E", "410E80", 0.0)]
    [InlineData("obd2.01.0F", "410F48", 32.0)]
    [InlineData("obd2.01.10", "411001F4", 5.0)]
    [InlineData("obd2.01.11", "411100", 0.0)]
    [InlineData("obd2.01.1F", "411F0102", 258.0)]
    [InlineData("obd2.01.21", "41210100", 256.0)]
    [InlineData("obd2.01.2F", "412FFF", 100.0)]
    [InlineData("obd2.01.31", "4131000A", 10.0)]
    [InlineData("obd2.01.33", "413365", 101.0)]
    [InlineData("obd2.01.42", "414236B0", 14.0)]
    [InlineData("obd2.01.46", "414628", 0.0)]
    [InlineData("obd2.01.5C", "415C82", 90.0)]
    public void LiveValue_is_decoded(string parameterId, string response, double expected)
    {
        Assert.True(Obd2PidCatalog.TryGet(parameterId, out var definition));

        var result = Obd2Parser.ParseLiveValue(Bytes(response), definition);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(expected, result.Value, 3);
    }

    [Theory]
    [InlineData("410C0C")]
    [InlineData("410C0C8000")]
    [InlineData("410D0C80")]
    [InlineData("430C0C80")]
    [InlineData("")]
    [InlineData("7F01")]
    [InlineData("7F0911")]
    public void Malformed_live_response_is_invalid(string response)
    {
        Assert.True(Obd2PidCatalog.TryGet("obd2.01.0C", out var definition));

        var result = Obd2Parser.ParseLiveValue(Bytes(response), definition);

        Assert.Equal(OperationStatus.InvalidResponse, result.Status);
    }

    [Theory]
    [InlineData("7F0111", OperationStatus.Unsupported)]
    [InlineData("7F0112", OperationStatus.Unsupported)]
    [InlineData("7F017E", OperationStatus.NotApplicable)]
    [InlineData("7F017F", OperationStatus.NotApplicable)]
    [InlineData("7F0131", OperationStatus.Failed)]
    [InlineData("7F0178", OperationStatus.Failed)]
    public void Negative_response_is_never_success_and_keeps_bytes(string response, OperationStatus expected)
    {
        Assert.True(Obd2PidCatalog.TryGet("obd2.01.0C", out var definition));

        var result = Obd2Parser.ParseLiveValue(Bytes(response), definition);

        Assert.Equal(expected, result.Status);
        Assert.Equal("NegativeResponse", result.ErrorCode);
        Assert.Equal(ServiceResponse.Hex(Bytes(response)), result.Message);
    }

    [Fact]
    public void Supported_pid_bitmap_is_decoded()
    {
        var result = Obd2Parser.ParseSupportedPids(Bytes("4100BE1FA813"), 0x00);

        Assert.NotNull(result.Value);
        Assert.Equal(
            "010304050607 0C0D0E0F10 111315 1C1F20".Replace(" ", ""),
            Convert.ToHexString(result.Value.Order().ToArray()));
    }

    [Fact]
    public void Supported_pid_response_with_wrong_base_is_invalid()
    {
        var result = Obd2Parser.ParseSupportedPids(Bytes("4120BE1FA813"), 0x00);

        Assert.Equal(OperationStatus.InvalidResponse, result.Status);
    }

    [Theory]
    [InlineData("490201")]
    [InlineData("4902")]
    public void Vin_is_read_with_and_without_item_count(string header)
    {
        byte[] response = [.. Bytes(header), .. Encoding.ASCII.GetBytes("SMLTD0000TEST0001")];

        var result = Obd2Parser.ParseVin(response);

        Assert.Equal("SMLTD0000TEST0001", result.Value);
    }

    [Fact]
    public void Vin_zero_padding_is_removed()
    {
        byte[] response = [0x49, 0x02, 0x01, 0x00, 0x00, .. Encoding.ASCII.GetBytes("SIMTEST00000001")];

        var result = Obd2Parser.ParseVin(response);

        Assert.Equal("SIMTEST00000001", result.Value);
    }

    [Theory]
    [InlineData("490201414243")]
    [InlineData("4902010000000000000000000000000000000000")]
    [InlineData("4902012020202020202020202020202020202020")]
    [InlineData("490401")]
    public void Invalid_vin_is_rejected(string response)
    {
        var result = Obd2Parser.ParseVin(Bytes(response));

        Assert.Equal(OperationStatus.InvalidResponse, result.Status);
        Assert.Null(result.Value);
    }

    [Theory]
    [InlineData(0x01, 0x00, "P0100")]
    [InlineData(0x41, 0x23, "C0123")]
    [InlineData(0x92, 0x34, "B1234")]
    [InlineData(0xC1, 0x58, "U0158")]
    public void Dtc_bytes_are_decoded(byte high, byte low, string expected)
    {
        Assert.Equal(expected, Obd2Parser.DecodeDtc(high, low));
    }

    [Theory]
    [InlineData("430201000300", "P0100,P0300")]
    [InlineData("4300", "")]
    [InlineData("43010003000000", "P0100,P0300")]
    public void Dtc_list_is_parsed(string response, string expected)
    {
        var result = Obd2Parser.ParseDtcs(Bytes(response), Obd2Service.StoredDtcs);

        Assert.NotNull(result.Value);
        Assert.Equal(expected, string.Join(',', result.Value));
    }

    [Theory]
    [InlineData("43")]
    [InlineData("43020100")]
    [InlineData("4700")]
    public void Invalid_dtc_response_is_rejected(string response)
    {
        var result = Obd2Parser.ParseDtcs(Bytes(response), Obd2Service.StoredDtcs);

        Assert.Equal(OperationStatus.InvalidResponse, result.Status);
    }

    [Fact]
    public void Catalog_ids_are_unique_and_unknown_ids_are_not_found()
    {
        var ids = Obd2PidCatalog.All.Select(d => d.ParameterId).ToArray();

        Assert.Equal(ids.Length, ids.Distinct().Count());
        Assert.False(Obd2PidCatalog.TryGet("obd2.01.FF", out _));
    }

    [Fact]
    public void Requests_are_built()
    {
        Assert.Equal("010C", Convert.ToHexString(Obd2Request.CurrentData(0x0C)));
        Assert.Equal("03", Convert.ToHexString(Obd2Request.StoredDtcs()));
        Assert.Equal("07", Convert.ToHexString(Obd2Request.PendingDtcs()));
        Assert.Equal("0902", Convert.ToHexString(Obd2Request.Vin()));
    }
}

using Contracts;

namespace Diagnostic.Protocols.Tests;

public class UdsTests
{
    private static byte[] Bytes(string hex) => Convert.FromHexString(hex);

    [Fact]
    public void Read_only_requests_are_built()
    {
        Assert.Equal("221234", Convert.ToHexString(UdsRequest.ReadDataByIdentifier(0x1234)));
        Assert.Equal("1902FF", Convert.ToHexString(UdsRequest.ReadDtcsByStatusMask(0xFF)));
    }

    [Fact]
    public void Data_by_identifier_returns_data_bytes()
    {
        var result = UdsParser.ParseDataByIdentifier(Bytes("621234AABB"), 0x1234);

        Assert.NotNull(result.Value);
        Assert.Equal("AABB", Convert.ToHexString(result.Value));
    }

    [Theory]
    [InlineData("629999AA")]
    [InlineData("621234")]
    [InlineData("501234AA")]
    public void Invalid_data_by_identifier_is_rejected(string response)
    {
        var result = UdsParser.ParseDataByIdentifier(Bytes(response), 0x1234);

        Assert.Equal(OperationStatus.InvalidResponse, result.Status);
    }

    [Fact]
    public void Dtc_records_are_parsed()
    {
        var result = UdsParser.ParseDtcsByStatusMask(Bytes("5902FF0123450C"));

        Assert.NotNull(result.Value);
        var record = Assert.Single(result.Value);
        Assert.Equal("P0123-45", record.Code);
        Assert.True(record.IsPending);
        Assert.True(record.IsConfirmed);
    }

    [Fact]
    public void Empty_dtc_list_is_success()
    {
        var result = UdsParser.ParseDtcsByStatusMask(Bytes("5902FF"));

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value);
    }

    [Theory]
    [InlineData("5902FF012345")]
    [InlineData("5901FF")]
    [InlineData("5902")]
    public void Invalid_dtc_information_is_rejected(string response)
    {
        var result = UdsParser.ParseDtcsByStatusMask(Bytes(response));

        Assert.Equal(OperationStatus.InvalidResponse, result.Status);
    }

    [Fact]
    public void Rejection_is_reported_with_original_bytes()
    {
        var result = UdsParser.ParseDtcsByStatusMask(Bytes("7F1931"));

        Assert.Equal(OperationStatus.Failed, result.Status);
        Assert.Equal("7F 19 31", result.Message);
    }
}

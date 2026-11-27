using Contracts;

namespace Diagnostic.Protocols.Tests;

public class Obd2FreezeFrameTests
{
    private static byte[] Bytes(string hex) => Convert.FromHexString(hex);

    [Fact]
    public void Request_holds_pid_and_frame_number()
    {
        Assert.Equal("025C01", Convert.ToHexString(Obd2FreezeFrame.Request(0x5C, 0x01)));
    }

    // Byte examples as used by the AOSP Obd2CommandTest (see SOURCES.md).
    [Theory]
    [InlineData("obd2.01.5C", "425C0187", 95.0)]
    [InlineData("obd2.01.0C", "420C01120F", 1155.75)]
    public void Value_is_decoded(string parameterId, string response, double expected)
    {
        Assert.True(Obd2PidCatalog.TryGet(parameterId, out var definition));

        var result = Obd2FreezeFrame.ParseValue(Bytes(response), 0x01, definition);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(expected, result.Value, 3);
    }

    [Theory]
    [InlineData("420C00120F")]
    [InlineData("420D01120F")]
    [InlineData("420C0112")]
    [InlineData("410C01120F")]
    public void Mismatched_value_response_is_invalid(string response)
    {
        Assert.True(Obd2PidCatalog.TryGet("obd2.01.0C", out var definition));

        var result = Obd2FreezeFrame.ParseValue(Bytes(response), 0x01, definition);

        Assert.Equal(OperationStatus.InvalidResponse, result.Status);
    }

    [Theory]
    [InlineData("4202000300", "P0300")]
    [InlineData("4202000000", "")]
    public void Trigger_code_is_parsed(string response, string expected)
    {
        var result = Obd2FreezeFrame.ParseTriggerDtc(Bytes(response), 0x00);

        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public void Rejection_keeps_original_bytes()
    {
        var result = Obd2FreezeFrame.ParseTriggerDtc(Bytes("7F0211"), 0x00);

        Assert.Equal(OperationStatus.Unsupported, result.Status);
        Assert.Equal("7F 02 11", result.Message);
    }
}

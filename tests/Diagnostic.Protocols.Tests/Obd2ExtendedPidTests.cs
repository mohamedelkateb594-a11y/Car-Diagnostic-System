using Contracts;

namespace Diagnostic.Protocols.Tests;

/// <summary>All byte sequences are synthetic test data.</summary>
public class Obd2ExtendedPidTests
{
    [Theory]
    [InlineData("obd2.01.22", "41220064", 7.9)]
    [InlineData("obd2.01.23", "41230064", 1000.0)]
    [InlineData("obd2.01.2C", "412CFF", 100.0)]
    [InlineData("obd2.01.2D", "412D80", 0.0)]
    [InlineData("obd2.01.2E", "412E00", 0.0)]
    [InlineData("obd2.01.30", "413007", 7.0)]
    [InlineData("obd2.01.3C", "413C0190", 0.0)]
    [InlineData("obd2.01.43", "414300FF", 100.0)]
    [InlineData("obd2.01.44", "41448000", 1.0)]
    [InlineData("obd2.01.45", "4145FF", 100.0)]
    [InlineData("obd2.01.47", "414700", 0.0)]
    [InlineData("obd2.01.4D", "414D0100", 256.0)]
    [InlineData("obd2.01.52", "4152FF", 100.0)]
    [InlineData("obd2.01.5A", "415AFF", 100.0)]
    [InlineData("obd2.01.5B", "415B00", 0.0)]
    [InlineData("obd2.01.5D", "415D6900", 0.0)]
    [InlineData("obd2.01.5E", "415E0064", 5.0)]
    public void LiveValue_is_decoded(string parameterId, string response, double expected)
    {
        Assert.True(Obd2PidCatalog.TryGet(parameterId, out var definition));

        var result = Obd2Parser.ParseLiveValue(Convert.FromHexString(response), definition);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(expected, result.Value, 3);
    }

    [Fact]
    public void Catalog_stays_within_the_queried_support_pages()
    {
        // The service reads the support pages 00, 20 and 40, which describe PIDs up to 60.
        Assert.All(Obd2PidCatalog.All, d => Assert.InRange((int)d.Pid, 0x01, 0x60));
        Assert.All(Obd2PidCatalog.All, d => Assert.InRange(d.Length, 1, 2));
    }
}

namespace Diagnostic.Presentation.Tests;

public class VinValidatorTests
{
    [Theory]
    [InlineData("SMLTD0000TEST0001", true)]
    [InlineData("ABCDEFGHJKLMNPRST", true)]
    [InlineData("1HGBH41JXMN10918I", false)]
    [InlineData("SHORT", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsWellFormed_ChecksFormatOnly(string? vin, bool expected)
        => Assert.Equal(expected, VinValidator.IsWellFormed(vin));
}

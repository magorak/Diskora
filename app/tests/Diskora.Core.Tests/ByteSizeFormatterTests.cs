using System.Globalization;
using Diskora.Core.Formatting;

namespace Diskora.Core.Tests;

public class ByteSizeFormatterTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1024, "1.00 KB")]
    [InlineData(1536, "1.50 KB")]
    [InlineData(1024L * 1024, "1.00 MB")]
    [InlineData(1024L * 1024 * 1024, "1.00 GB")]
    [InlineData(1024L * 1024 * 1024 * 1024, "1.00 TB")]
    public void Format_ProducesExpectedUnitAndPrecision(long bytes, string expected)
    {
        var result = ByteSizeFormatter.Format(bytes, CultureInfo.InvariantCulture);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Format_NegativeValue_KeepsSignAndFormatsMagnitude()
    {
        var result = ByteSizeFormatter.Format(-2048, CultureInfo.InvariantCulture);

        Assert.Equal("-2.00 KB", result);
    }

    [Fact]
    public void Format_ValueBeyondPetabyteUnit_DoesNotOverflowUnitTable()
    {
        var oneExabyteInBytes = 1024L * 1024 * 1024 * 1024 * 1024 * 1024;

        var result = ByteSizeFormatter.Format(oneExabyteInBytes, CultureInfo.InvariantCulture);

        Assert.Equal("1,024.00 PB", result);
    }
}

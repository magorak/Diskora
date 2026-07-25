using Diskora.App.Parsing;

namespace Diskora.App.Tests;

public class ChkdskOutputParserTests
{
    [Theory]
    [InlineData("Stage 1: Examining basic file system structure ...", 1)]
    [InlineData("Stage 2: Examining file name linkage ...", 2)]
    [InlineData("Stage 3: Examining security descriptors ...", 3)]
    [InlineData("stage 4: Looking for bad clusters in user file data ...", 4)]
    public void TryParseStage_KnownStageLine_ReturnsStageNumber(string line, int expectedStage)
    {
        var stage = ChkdskOutputParser.TryParseStage(line);

        Assert.Equal(expectedStage, stage);
    }

    [Theory]
    [InlineData("The type of the file system is NTFS.")]
    [InlineData("Přístup byl odepřen.")]
    [InlineData("  256 file records processed.")]
    public void TryParseStage_UnrelatedLine_ReturnsNull(string line)
    {
        var stage = ChkdskOutputParser.TryParseStage(line);

        Assert.Null(stage);
    }

    [Theory]
    [InlineData(1, "Kontrola základní struktury systému souborů")]
    [InlineData(2, "Kontrola provázání názvů souborů")]
    [InlineData(3, "Kontrola deskriptorů zabezpečení")]
    public void GetStageDescription_KnownStage_ReturnsCzechDescription(int stage, string expected)
    {
        var description = ChkdskOutputParser.GetStageDescription(stage);

        Assert.Equal(expected, description);
    }

    [Fact]
    public void GetStageDescription_UnknownStage_FallsBackToGenericLabel()
    {
        var description = ChkdskOutputParser.GetStageDescription(99);

        Assert.Equal("Fáze 99", description);
    }

    [Theory]
    [InlineData("  10 percent complete.", 10)]
    [InlineData("100 percent complete.", 100)]
    [InlineData("5 PERCENT COMPLETE.", 5)]
    public void TryParsePercent_KnownPercentLine_ReturnsValue(string line, int expected)
    {
        var percent = ChkdskOutputParser.TryParsePercent(line);

        Assert.Equal(expected, percent);
    }

    [Fact]
    public void TryParsePercent_UnrelatedLine_ReturnsNull()
    {
        var percent = ChkdskOutputParser.TryParsePercent("Stage 1: Examining basic file system structure ...");

        Assert.Null(percent);
    }
}

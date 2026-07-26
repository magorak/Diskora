using Diskora.Core.Export;

namespace Diskora.Core.Tests;

public class CsvWriterTests
{
    [Fact]
    public void Write_SimpleValues_ProducesHeaderAndRowsWithCrLf()
    {
        var csv = CsvWriter.Write(
            ["Název", "Velikost"],
            [["fileA.txt", "100"], ["fileB.txt", "200"]]);

        Assert.Equal("Název,Velikost\r\nfileA.txt,100\r\nfileB.txt,200\r\n", csv);
    }

    [Fact]
    public void Write_ValueContainingComma_IsQuoted()
    {
        var csv = CsvWriter.Write(["Název"], [["a, b.txt"]]);

        Assert.Equal("Název\r\n\"a, b.txt\"\r\n", csv);
    }

    [Fact]
    public void Write_ValueContainingQuote_IsQuotedAndDoubled()
    {
        var csv = CsvWriter.Write(["Název"], [["12\" disk.iso"]]);

        Assert.Equal("Název\r\n\"12\"\" disk.iso\"\r\n", csv);
    }

    [Fact]
    public void Write_ValueContainingNewline_IsQuoted()
    {
        var csv = CsvWriter.Write(["Popis"], [["řádek 1\nřádek 2"]]);

        Assert.Equal("Popis\r\n\"řádek 1\nřádek 2\"\r\n", csv);
    }

    [Fact]
    public void Write_NoRows_ProducesOnlyHeader()
    {
        var csv = CsvWriter.Write(["A", "B"], []);

        Assert.Equal("A,B\r\n", csv);
    }
}

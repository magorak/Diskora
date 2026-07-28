using Diskora.Core.Diagnostics;

namespace Diskora.Core.Tests;

public class CapacityTestPatternTests
{
    [Fact]
    public void Fill_NaplniPodlePozice_NezavisleNaDeleniDoBloku()
    {
        // Stejný úsek disku musí vyjít stejně bez ohledu na to, jak velké
        // bloky se zrovna zapisují - jinak by ověření hlásilo falešné chyby.
        var wholeBlock = new byte[512];
        CapacityTestPattern.Fill(wholeBlock, 1000);

        var firstHalf = new byte[256];
        var secondHalf = new byte[256];
        CapacityTestPattern.Fill(firstHalf, 1000);
        CapacityTestPattern.Fill(secondHalf, 1256);

        Assert.Equal(wholeBlock[..256], firstHalf);
        Assert.Equal(wholeBlock[256..], secondHalf);
    }

    [Fact]
    public void FindFirstMismatch_SpravnaDataProjdou()
    {
        var buffer = new byte[4096];
        CapacityTestPattern.Fill(buffer, 8192);

        Assert.Null(CapacityTestPattern.FindFirstMismatch(buffer, 8192));
    }

    [Fact]
    public void FindFirstMismatch_VratiAbsolutniPoziciPrvnihoRozdilu()
    {
        var buffer = new byte[4096];
        CapacityTestPattern.Fill(buffer, 8192);
        buffer[100] ^= 0xFF;
        buffer[200] ^= 0xFF;

        Assert.Equal(8192 + 100, CapacityTestPattern.FindFirstMismatch(buffer, 8192));
    }

    [Fact]
    public void FindFirstMismatch_PoznaDataZJinePozice()
    {
        // Přesně to dělá přeznačený disk: zápis nad skutečnou kapacitou se
        // „zabalí" zpátky, takže se na dané pozici čtou data patřící jinam.
        const long fakeOffset = 32L * 1024 * 1024 * 1024;
        var buffer = new byte[4096];
        CapacityTestPattern.Fill(buffer, 0);

        // Nesoulad se pozná hned na prvním bajtu; vrací se absolutní pozice.
        Assert.Equal(fakeOffset, CapacityTestPattern.FindFirstMismatch(buffer, fakeOffset));
    }

    [Fact]
    public void FindFirstMismatch_PoznaISamychNul()
    {
        // Disk, který nad svou kapacitou vrací nuly, nesmí projít.
        var zeros = new byte[4096];

        Assert.NotNull(CapacityTestPattern.FindFirstMismatch(zeros, 1_000_000));
    }

    [Fact]
    public void ByteAt_SousedniPoziceSeVyraznehLisi()
    {
        // Kdyby se sousedi lišili jen v jednom bitu, posunutá data by mohla projít.
        var values = Enumerable.Range(0, 64).Select(i => CapacityTestPattern.ByteAt(i)).ToList();

        Assert.True(values.Distinct().Count() > 40, "Vzor je málo rozmanitý.");
    }

    [Fact]
    public void ByteAt_JeDeterministicky()
    {
        Assert.Equal(CapacityTestPattern.ByteAt(123_456_789), CapacityTestPattern.ByteAt(123_456_789));
    }

    [Fact]
    public void Fill_PrazdnyBufferNespadne()
    {
        CapacityTestPattern.Fill(Span<byte>.Empty, 42);

        Assert.Null(CapacityTestPattern.FindFirstMismatch(ReadOnlySpan<byte>.Empty, 42));
    }
}

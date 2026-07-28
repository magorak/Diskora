using Diskora.Core.Output;

namespace Diskora.Core.Tests;

/// <summary>
/// Vstupy jsou doslovné řádky ze SKUTEČNÉHO běhu `chkdsk H: /scan` a
/// `defrag H: /D` na testovacím stroji, ne vymyšlené ukázky.
/// </summary>
public class ToolOutputTranslatorTests
{
    [Fact]
    public void Translate_NeznamyRadekZustaneBeZmeny()
    {
        // Nová verze Windows nesmí způsobit ztrátu informace.
        const string line = "Some entirely unexpected diagnostic line from a future Windows.";

        Assert.Equal(line, ToolOutputTranslator.Translate(line));
    }

    [Fact]
    public void Translate_ZachovaOdsazeni()
    {
        // Odsazení nese strukturu reportu defragu.
        var translated = ToolOutputTranslator.Translate("\t\tVolume size                 = 298.08 GB");

        Assert.StartsWith("\t\t", translated, StringComparison.Ordinal);
        Assert.Contains("Velikost svazku", translated, StringComparison.Ordinal);
        Assert.Contains("298.08 GB", translated, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("File verification completed.", "Ověření souborů dokončeno.")]
    [InlineData("Windows has scanned the file system and found no problems.",
        "Windows prošly souborový systém a nenašly žádné problémy.")]
    [InlineData("No further action is required.", "Není potřeba nic dalšího dělat.")]
    [InlineData("The operation completed successfully.", "Operace proběhla úspěšně.")]
    [InlineData("You do not need to defragment this volume.", "Tento svazek není potřeba defragmentovat.")]
    public void Translate_CeleVety(string input, string expected)
    {
        Assert.Equal(expected, ToolOutputTranslator.Translate(input));
    }

    [Fact]
    public void Translate_TypSouborovehoSystemuANazevSvazku()
    {
        Assert.Equal("Souborový systém: NTFS", ToolOutputTranslator.Translate("The type of the file system is NTFS."));
        Assert.Equal("Název svazku: Nový svazek", ToolOutputTranslator.Translate("Volume label is Nový svazek."));
    }

    [Theory]
    [InlineData("  403200 file records processed.", "Zpracováno záznamů souborů: 403200")]
    [InlineData("  502620 index entries processed.", "Zpracováno položek indexu: 502620")]
    [InlineData("  11 reparse records processed.", "Zpracováno reparse záznamů: 11")]
    [InlineData("  0 bad file records processed.", "Zpracováno vadných záznamů souborů: 0")]
    [InlineData("  49710 data files processed.", "Zpracováno datových souborů: 49710")]
    public void Translate_PocitaneRadky(string input, string expected)
    {
        Assert.Equal(expected, ToolOutputTranslator.Translate(input).Trim());
    }

    [Fact]
    public void Translate_SouhrnMistaNaDisku()
    {
        Assert.Equal("Celková velikost disku: 312568831 KB",
            ToolOutputTranslator.Translate(" 312568831 KB total disk space.").Trim());
        Assert.Equal("Ve vadných sektorech: 0 KB",
            ToolOutputTranslator.Translate("         0 KB in bad sectors.").Trim());
        Assert.Equal("Volné místo na disku: 200332 KB",
            ToolOutputTranslator.Translate("    200332 KB available on disk.").Trim());
    }

    [Fact]
    public void Translate_MistoVSouborechIIndexech_ZachovaObeCisla()
    {
        Assert.Equal("V souborech: 311797224 KB (353431)",
            ToolOutputTranslator.Translate(" 311797224 KB in 353431 files.").Trim());
        Assert.Equal("V indexech: 92536 KB (49712)",
            ToolOutputTranslator.Translate("     92536 KB in 49712 indexes.").Trim());
    }

    [Fact]
    public void Translate_AlokacniJednotky()
    {
        Assert.Equal("Velikost alokační jednotky: 4096 bajtů",
            ToolOutputTranslator.Translate("      4096 bytes in each allocation unit.").Trim());
        Assert.Equal("Celkem alokačních jednotek: 78142207",
            ToolOutputTranslator.Translate("  78142207 total allocation units on disk.").Trim());
    }

    [Fact]
    public void Translate_DobaFaze_PrelozeIJednotky()
    {
        Assert.Equal("Doba fáze (ověření indexů): 1.06 min",
            ToolOutputTranslator.Translate(" Phase duration (Index verification): 1.06 minutes.").Trim());
        Assert.Equal("Doba fáze (kontrola vadných záznamů souborů): 0.02 ms",
            ToolOutputTranslator.Translate(" Phase duration (Bad file record checking): 0.02 milliseconds.").Trim());
    }

    [Fact]
    public void Translate_NeznamyNazevFaze_ZustaneAnglicky()
    {
        // Radši ponechat původní název než ho zamlčet.
        var translated = ToolOutputTranslator.Translate(" Phase duration (Something New): 1.00 seconds.").Trim();

        Assert.Equal("Doba fáze (Something New): 1.00 s", translated);
    }

    [Fact]
    public void Translate_SpousteniNastroje()
    {
        Assert.Equal("Spouštím defragmentaci: Nový svazek (H:)...",
            ToolOutputTranslator.Translate("Invoking defragmentation on Nový svazek (H:)..."));
        Assert.Equal("Spouštím TRIM: Data (F:)...",
            ToolOutputTranslator.Translate("Invoking retrim on Data (F:)..."));
    }

    [Theory]
    [InlineData("\t\tFree space                  = 191.66 MB", "Volné místo = 191.66 MB")]
    [InlineData("\t\tFragmented files            = 4", "Fragmentované soubory = 4")]
    [InlineData("\t\tMFT usage                   = 100%", "Využití MFT = 100%")]
    [InlineData("\t\tAverage free space size     = 0 bytes", "Průměrná velikost volné oblasti = 0 bajtů")]
    public void Translate_PopiskyReportuDefragu(string input, string expected)
    {
        Assert.Equal(expected, ToolOutputTranslator.Translate(input).Trim());
    }

    [Fact]
    public void Translate_NeznamyPopisekSRovnitkem_ZustaneBeZmeny()
    {
        const string line = "\t\tSome future metric          = 42";

        Assert.Equal(line, ToolOutputTranslator.Translate(line));
    }

    [Theory]
    [InlineData("Stage 1: Examining basic file system structure ...", "Fáze 1: kontrola základní struktury systému souborů...")]
    [InlineData("Stage 2: Examining file name linkage ...", "Fáze 2: kontrola provázání názvů souborů...")]
    [InlineData("Stage 3: Examining security descriptors ...", "Fáze 3: kontrola deskriptorů zabezpečení...")]
    public void Translate_HlavickyFazi(string input, string expected)
    {
        Assert.Equal(expected, ToolOutputTranslator.Translate(input));
    }

    [Fact]
    public void Translate_NeznamaFaze_PonechaPuvodniPopis()
    {
        Assert.Equal("Fáze 9: Doing something new...",
            ToolOutputTranslator.Translate("Stage 9: Doing something new ..."));
    }

    [Fact]
    public void IsProgressNoise_PoznaZaplavoveRadky()
    {
        Assert.True(ToolOutputTranslator.IsProgressNoise(
            "Progress: 18177 of 403200 done; Stage:  4%; Total:  2%; ETA:   0:19:07 .  "));
        Assert.False(ToolOutputTranslator.IsProgressNoise("File verification completed."));
        Assert.False(ToolOutputTranslator.IsProgressNoise("Stage 1: Examining basic file system structure ..."));
    }

    [Fact]
    public void IsProgressNoise_DlouhyRadekSamychMezerJeZaplava()
    {
        // Chkdsk "maže" předchozí řádek postupu tím, že vypíše řádek plný mezer.
        Assert.True(ToolOutputTranslator.IsProgressNoise(new string(' ', 87)));

        // Krátký prázdný řádek je ale legitimní oddělovač odstavců.
        Assert.False(ToolOutputTranslator.IsProgressNoise(string.Empty));
        Assert.False(ToolOutputTranslator.IsProgressNoise("   "));
    }

    [Fact]
    public void Translate_PrazdnyRadek_Projde()
    {
        Assert.Equal("   ", ToolOutputTranslator.Translate("   "));
        Assert.Equal(string.Empty, ToolOutputTranslator.Translate(string.Empty));
    }

    [Fact]
    public void Translate_ZnackaUspesneOpravy()
    {
        Assert.Equal("Oprava dokončena - žádné chyby k opravě nebyly nalezeny.",
            ToolOutputTranslator.Translate("DISKORA_STATUS: NoErrorsFound"));
    }

    [Fact]
    public void Translate_ZnackaChybyOpravy()
    {
        Assert.Equal("Oprava selhala: The repair failed",
            ToolOutputTranslator.Translate("DISKORA_ERROR: The repair failed"));
    }

    [Fact]
    public void Translate_ZnackaChybejicihoVysledku_VysvetliPravdepodobnouPricinu()
    {
        var text = ToolOutputTranslator.Translate("DISKORA_ERROR: NO_RESULT");

        Assert.Contains("práva administrátora", text, StringComparison.Ordinal);
    }
}

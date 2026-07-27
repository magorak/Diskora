using Diskora.Core.Changelog;

namespace Diskora.Core.Tests;

public class ChangelogParserTests
{
    private const string Sample = """
        # Changelog

        Úvodní odstavec, který do žádné verze nepatří.

        ## [Unreleased]

        ### Opraveno
        - První oprava.
        - Druhá oprava, která pokračuje
          na dalším řádku
          a ještě na jednom.

        ### Přidáno
        - Něco nového s `NazvemTridy` uvnitř.

        ## [0.1.0] - 2026-07-25

        ### Přidáno
        - Úplně první verze.
        """;

    [Fact]
    public void Parse_RozdeliVerzeIKategorie()
    {
        var releases = ChangelogParser.Parse(Sample);

        Assert.Equal(2, releases.Count);
        Assert.Equal("[Unreleased]", releases[0].Title);
        Assert.Equal("[0.1.0] - 2026-07-25", releases[1].Title);
        Assert.Equal(["Opraveno", "Přidáno"], releases[0].Sections.Select(s => s.Category));
    }

    [Fact]
    public void Parse_SlepiOdsazenePokracovaciRadkyDoJednePolozky()
    {
        var items = ChangelogParser.Parse(Sample)[0].Sections[0].Items;

        Assert.Equal(2, items.Count);
        Assert.Equal("Druhá oprava, která pokračuje na dalším řádku a ještě na jednom.", items[1]);
    }

    [Fact]
    public void Parse_TextMimoVerziSeIgnoruje()
    {
        // Nadpis souboru a úvodní odstavec nejsou součástí žádné verze.
        var releases = ChangelogParser.Parse(Sample);

        Assert.DoesNotContain(releases, r => r.Title.Contains("Changelog", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_VerzeBezPolozekSeVynecha()
    {
        var releases = ChangelogParser.Parse("## [Unreleased]\n\n### Přidáno\n\n## [0.1.0]\n\n### Přidáno\n- Něco.\n");

        Assert.Single(releases);
        Assert.Equal("[0.1.0]", releases[0].Title);
    }

    [Fact]
    public void Parse_PrazdnyVstupNespadne()
    {
        Assert.Empty(ChangelogParser.Parse(string.Empty));
    }

    [Fact]
    public void Parse_ZvladneObaTvaryKoncuRadku()
    {
        var windows = ChangelogParser.Parse("## [0.1.0]\r\n\r\n### Přidáno\r\n- Něco.\r\n");
        var unix = ChangelogParser.Parse("## [0.1.0]\n\n### Přidáno\n- Něco.\n");

        Assert.Equal("Něco.", windows[0].Sections[0].Items[0]);
        Assert.Equal(unix[0].Sections[0].Items[0], windows[0].Sections[0].Items[0]);
    }

    [Fact]
    public void ParseInline_OddeliZpetneUvozovky()
    {
        var segments = ChangelogParser.ParseInline("Něco nového s `NazvemTridy` uvnitř.");

        Assert.Equal(3, segments.Count);
        Assert.Equal(("Něco nového s ", false), (segments[0].Text, segments[0].IsCode));
        Assert.Equal(("NazvemTridy", true), (segments[1].Text, segments[1].IsCode));
        Assert.Equal((" uvnitř.", false), (segments[2].Text, segments[2].IsCode));
    }

    [Fact]
    public void Parse_SlejeOpakovaneKategorieVRamciVerze()
    {
        // Reálný CHANGELOG.md má v „Unreleased" víc bloků „### Přidáno" za sebou,
        // protože si každý commit přidá vlastní - čtenář má ale vidět jednu
        // kategorii se všemi položkami, ne deset nadpisů dokola.
        var releases = ChangelogParser.Parse("""
            ## [Unreleased]

            ### Opraveno
            - Oprava A.

            ### Přidáno
            - Novinka A.

            ### Opraveno
            - Oprava B.
            """);

        var sections = releases.Single().Sections;
        Assert.Equal(["Opraveno", "Přidáno"], sections.Select(s => s.Category));
        Assert.Equal(["Oprava A.", "Oprava B."], sections[0].Items);
    }

    [Fact]
    public void Parse_StejnaKategorieVRuznychVerzichSeNesleva()
    {
        var releases = ChangelogParser.Parse("""
            ## [0.2.0]

            ### Přidáno
            - Novější.

            ## [0.1.0]

            ### Přidáno
            - Starší.
            """);

        Assert.Equal(["Novější."], releases[0].Sections.Single().Items);
        Assert.Equal(["Starší."], releases[1].Sections.Single().Items);
    }

    [Fact]
    public void ParseInline_ZvyrazneniHvezdickami()
    {
        var segments = ChangelogParser.ParseInline("Tohle je **hodně důležité** tvrzení.");

        Assert.Equal(3, segments.Count);
        Assert.False(segments[0].IsBold);
        Assert.True(segments[1].IsBold);
        Assert.Equal("hodně důležité", segments[1].Text);
        Assert.False(segments[2].IsBold);
    }

    [Fact]
    public void ParseInline_NeparovaHvezdickaZustaneTextem()
    {
        var segments = ChangelogParser.ParseInline("Násobení 2 ** 3 bez uzavření");

        Assert.Single(segments);
        Assert.False(segments[0].IsBold);
        Assert.Equal("Násobení 2 ** 3 bez uzavření", segments[0].Text);
    }

    [Fact]
    public void ParseInline_ZvyrazneniIKodVJedneVete()
    {
        var segments = ChangelogParser.ParseInline("**Pozor:** volání `DeviceIoControl` selže.");

        Assert.Contains(segments, s => s.IsBold && s.Text == "Pozor:");
        Assert.Contains(segments, s => s.IsCode && s.Text == "DeviceIoControl");
    }

    [Fact]
    public void ParseInline_NeparovaUvozovkaNespolkneZbytek()
    {
        var segments = ChangelogParser.ParseInline("Cesta C:\\Program Files nebo ` osamocená uvozovka");

        Assert.Single(segments);
        Assert.False(segments[0].IsCode);
        Assert.Contains("osamocená", segments[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseInline_ViceUsekuKoduZaSebou()
    {
        var segments = ChangelogParser.ParseInline("`a` a `b`");

        Assert.Equal([true, false, true], segments.Select(s => s.IsCode));
    }

    [Fact]
    public void ParseInline_TextBezKoduJeJedinySegment()
    {
        var segments = ChangelogParser.ParseInline("Obyčejná věta.");

        Assert.Single(segments);
        Assert.False(segments[0].IsCode);
    }
}

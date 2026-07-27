using Diskora.Core.Models;

namespace Diskora.Core.Changelog;

/// <summary>
/// Rozloží `CHANGELOG.md` na verze, kategorie a položky. Rozumí jen tvaru, který
/// tenhle jeden soubor skutečně používá (Keep a Changelog: „## verze" /
/// „### kategorie" / „- položka" s odsazenými pokračovacími řádky) - není to
/// obecný markdown engine, jen tolik, kolik je potřeba. Záměrně stejná pravidla
/// jako parser ve `web/src/pages/docs/changelog.astro`: obě verze changelogu
/// (v aplikaci i na webu) čtou týž soubor, takže se nemůžou rozejít s realitou.
/// </summary>
public static class ChangelogParser
{
    public static IReadOnlyList<ChangelogRelease> Parse(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var releases = new List<ChangelogRelease>();
        List<ChangelogSection>? sections = null;
        Dictionary<string, List<string>>? itemsByCategory = null;
        List<string>? items = null;
        List<string>? currentItem = null;

        void FlushItem()
        {
            if (currentItem is not null && items is not null)
            {
                var text = string.Join(" ", currentItem).Trim();
                if (text.Length > 0)
                {
                    items.Add(text);
                }
            }

            currentItem = null;
        }

        foreach (var rawLine in markdown.ReplaceLineEndings("\n").Split('\n'))
        {
            var line = rawLine.TrimEnd();

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                FlushItem();
                sections = [];
                itemsByCategory = [];
                items = null;
                releases.Add(new ChangelogRelease(line[3..].Trim(), sections));
            }
            else if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                FlushItem();
                var category = line[4..].Trim();

                // Jedna verze má v CHANGELOG.md běžně víc bloků téže kategorie
                // (každý commit si přidá vlastní „### Přidáno"). Pro čtenáře je
                // to jedna kategorie, ne deset - položky se proto slévají do
                // prvního výskytu místo opakování nadpisu dokola.
                if (itemsByCategory is not null && !itemsByCategory.TryGetValue(category, out items))
                {
                    items = [];
                    itemsByCategory[category] = items;
                    sections?.Add(new ChangelogSection(category, items));
                }
            }
            else if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                FlushItem();
                currentItem = [line[2..].Trim()];
            }
            else if (currentItem is not null && line.Length > 0 && char.IsWhiteSpace(line[0]))
            {
                // Odsazený řádek pokračuje v předchozí položce - v CHANGELOG.md
                // jsou položky často na deset řádků kvůli šířce textu.
                currentItem.Add(line.Trim());
            }
            else
            {
                FlushItem();
            }
        }

        FlushItem();

        // Verze bez jediné položky (jen nadpis, typicky rozpracovaná sekce) by
        // v okně byly prázdné řádky - do výsledku nepatří.
        return releases.Where(r => r.Sections.Any(s => s.Items.Count > 0)).ToList();
    }

    /// <summary>
    /// Rozseká text položky na běžný text, úseky v kódu (mezi zpětnými uvozovkami)
    /// a zvýrazněné úseky (mezi dvojicemi hvězdiček). Nepárový oddělovač se bere
    /// jako obyčejný znak, ne jako začátek úseku, který by spolkl zbytek věty -
    /// v changelogu se běžně vyskytnou samostatné hvězdičky i uvozovky.
    /// Vnořování (kód uvnitř zvýraznění) se neřeší, changelog ho nepoužívá.
    /// </summary>
    public static IReadOnlyList<ChangelogSegment> ParseInline(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var segments = new List<ChangelogSegment>();
        var plain = new System.Text.StringBuilder();
        var position = 0;

        while (position < text.Length)
        {
            if (text[position] == '`')
            {
                var close = text.IndexOf('`', position + 1);
                if (close > 0)
                {
                    Flush();
                    Add(text[(position + 1)..close], isCode: true, isBold: false);
                    position = close + 1;
                    continue;
                }
            }
            else if (text[position] == '*' && position + 1 < text.Length && text[position + 1] == '*')
            {
                var close = text.IndexOf("**", position + 2, StringComparison.Ordinal);
                if (close > 0)
                {
                    Flush();
                    Add(text[(position + 2)..close], isCode: false, isBold: true);
                    position = close + 2;
                    continue;
                }
            }

            plain.Append(text[position]);
            position++;
        }

        Flush();
        return segments;

        void Flush()
        {
            Add(plain.ToString(), isCode: false, isBold: false);
            plain.Clear();
        }

        void Add(string value, bool isCode, bool isBold)
        {
            if (value.Length > 0)
            {
                segments.Add(new ChangelogSegment(value, isCode, isBold));
            }
        }
    }
}

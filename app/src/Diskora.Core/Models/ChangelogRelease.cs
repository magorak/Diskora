namespace Diskora.Core.Models;

/// <summary>Jedna verze z `CHANGELOG.md` (nadpis „## ..." a všechno pod ním až k další verzi).</summary>
public sealed record ChangelogRelease(
    string Title,
    IReadOnlyList<ChangelogSection> Sections);

/// <summary>Kategorie změn uvnitř verze (nadpis „### Přidáno"/„### Opraveno" a její položky).</summary>
public sealed record ChangelogSection(
    string Category,
    IReadOnlyList<string> Items);

/// <summary>
/// Kousek textu položky changelogu. Text psaný v markdownu mezi zpětnými
/// uvozovkami (názvy tříd, souborů, příkazů) je označený jako kód a text mezi
/// dvojicemi hvězdiček jako zvýrazněný - ať se dají vysázet odlišně, stejně jako
/// `&lt;code&gt;`/`&lt;strong&gt;` na webové verzi changelogu.
/// </summary>
public sealed record ChangelogSegment(string Text, bool IsCode, bool IsBold = false);

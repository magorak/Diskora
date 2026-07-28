using System.Text.RegularExpressions;

namespace Diskora.App.Parsing;

/// <summary>
/// Chkdsk vypisuje fáze a procenta pevně anglicky bez ohledu na jazyk Windows
/// (ověřeno živě - i na české lokalizaci se objevilo "The type of the file
/// system is NTFS." vedle českých hlášek). Překládat celý syrový výstup by
/// bylo křehké a nikdy kompletní, takže místo toho z něj vytáhneme jen fázi
/// a procento a k nim ukážeme vlastní srozumitelný český popisek a progress
/// bar - syrový log zůstává jako doplňkový detail pro řešení problémů.
/// </summary>
public static partial class ChkdskOutputParser
{
    private static readonly Dictionary<int, string> StageDescriptionsCs = new()
    {
        [1] = "Kontrola základní struktury systému souborů",
        [2] = "Kontrola provázání názvů souborů",
        [3] = "Kontrola deskriptorů zabezpečení",
        [4] = "Kontrola dat souborů na vadné sektory",
        [5] = "Kontrola volných sektorů",
    };

    public static int? TryParseStage(string line)
    {
        var match = StageRegex().Match(line);
        return match.Success && int.TryParse(match.Groups[1].Value, out var stage) ? stage : null;
    }

    public static string GetStageDescription(int stage) =>
        StageDescriptionsCs.TryGetValue(stage, out var description) ? description : $"Fáze {stage}";

    public static int? TryParsePercent(string line)
    {
        var match = PercentRegex().Match(line);
        return match.Success && int.TryParse(match.Groups[1].Value, out var percent) ? percent : null;
    }

    /// <summary>
    /// Celkový postup napříč všemi fázemi z řádku
    /// „Progress: X of Y done; Stage:  N%; Total:  M%; ETA: ...".
    ///
    /// Tenhle tvar vypisuje chkdsk na aktuálních Windows - starší formát
    /// „N percent complete", na který se dívá <see cref="TryParsePercent"/>,
    /// se tu vůbec neobjeví. Progress bar proto dřív skákal jen po hranicích
    /// fází (0/33/66 %) a uvnitř fáze stál. Hodnota „Total" je navíc lepší než
    /// vlastní dopočítávání z fáze - chkdsk ví, jak dlouhé fáze budou.
    /// </summary>
    public static int? TryParseOverallPercent(string line)
    {
        var match = OverallPercentRegex().Match(line);
        return match.Success && int.TryParse(match.Groups[1].Value, out var percent)
            ? Math.Clamp(percent, 0, 100)
            : null;
    }

    [GeneratedRegex(@"Total:\s*(\d{1,3})\s*%", RegexOptions.IgnoreCase)]
    private static partial Regex OverallPercentRegex();

    [GeneratedRegex(@"^Stage (\d+):", RegexOptions.IgnoreCase)]
    private static partial Regex StageRegex();

    [GeneratedRegex(@"(\d{1,3})\s*percent complete", RegexOptions.IgnoreCase)]
    private static partial Regex PercentRegex();
}

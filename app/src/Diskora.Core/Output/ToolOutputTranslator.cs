using System.Text.RegularExpressions;

namespace Diskora.Core.Output;

/// <summary>
/// Překládá výstup orchestrovaných nástrojů (`chkdsk.exe`, `defrag.exe`) do češtiny.
/// Oba nástroje píšou pevně anglicky bez ohledu na jazyk Windows, takže uprostřed
/// jinak české aplikace svítil anglický blok textu (nahlásil uživatel).
///
/// Principy:
/// - Překládá se jen to, co je bezpečně rozpoznané. Neznámý řádek se vrací
///   beze změny, takže nová verze Windows nikdy nezpůsobí ztrátu informace.
/// - Čísla, jednotky a názvy svazků se nikdy nepřepisují, jen se kolem nich
///   vymění text.
/// - Odsazení původního řádku se zachovává, aby zůstala čitelná struktura
///   odsazených reportů defragu.
/// - Volající si vždy může nechat i syrový anglický originál (viz `IsProgressNoise`
///   a přepínač v UI) - překlad není náhrada, ale výchozí zobrazení.
/// </summary>
public static partial class ToolOutputTranslator
{
    /// <summary>Celé věty, které nemají žádnou proměnnou část.</summary>
    private static readonly Dictionary<string, string> Phrases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["File verification completed."] = "Ověření souborů dokončeno.",
        ["Index verification completed."] = "Ověření indexů dokončeno.",
        ["Security descriptor verification completed."] = "Ověření deskriptorů zabezpečení dokončeno.",
        ["Usn Journal verification completed."] = "Ověření žurnálu USN dokončeno.",
        ["Windows has scanned the file system and found no problems."] = "Windows prošly souborový systém a nenašly žádné problémy.",
        ["Windows has made corrections to the file system."] = "Windows provedly opravy souborového systému.",
        ["Windows found problems with the file system."] = "Windows našly problémy v souborovém systému.",
        ["No further action is required."] = "Není potřeba nic dalšího dělat.",
        ["Run CHKDSK with the /F (fix) option to correct these."] = "Opravu spustíte příkazem CHKDSK s přepínačem /F.",
        ["The operation completed successfully."] = "Operace proběhla úspěšně.",
        ["You do not need to defragment this volume."] = "Tento svazek není potřeba defragmentovat.",
        ["Pre-Optimization Report:"] = "Zpráva před optimalizací:",
        ["Post Defragmentation Report:"] = "Zpráva po defragmentaci:",
        ["Volume Information:"] = "Informace o svazku:",
        ["Fragmentation:"] = "Fragmentace:",
        ["Files:"] = "Soubory:",
        ["Folders:"] = "Složky:",
        ["Free space:"] = "Volné místo:",
        ["Master File Table (MFT):"] = "Hlavní tabulka souborů (MFT):",
        ["A snapshot error occured while scanning this drive. Run an offline scan and fix."] =
            "Nepodařilo se vytvořit snímek svazku, takže kontrolu za běhu nelze dokončit. "
            + "Svazek jde zkontrolovat jen odpojený - u vyměnitelných disků pomůže odpojit je od ostatních programů.",
        ["Windows cannot run disk checking on this volume because it is write protected."] =
            "Svazek je chráněný proti zápisu, takže na něm kontrola nemůže běžet.",
        ["Note: File fragments larger than 64MB are not included in the fragmentation statistics."] =
            "Poznámka: Fragmenty souborů větší než 64 MB se do statistik fragmentace nepočítají.",
    };

    /// <summary>Popisky ve tvaru „Název = hodnota" v reportech defragu.</summary>
    private static readonly Dictionary<string, string> Labels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Volume size"] = "Velikost svazku",
        ["Cluster size"] = "Velikost clusteru",
        ["Used space"] = "Využité místo",
        ["Free space"] = "Volné místo",
        ["Total fragmented space"] = "Celkem fragmentovaného místa",
        ["Average fragments per file"] = "Průměrný počet fragmentů na soubor",
        ["Movable files and folders"] = "Přesouvatelné soubory a složky",
        ["Unmovable files and folders"] = "Nepřesouvatelné soubory a složky",
        ["Fragmented files"] = "Fragmentované soubory",
        ["Total file fragments"] = "Celkem fragmentů souborů",
        ["Total folders"] = "Celkem složek",
        ["Fragmented folders"] = "Fragmentované složky",
        ["Total folder fragments"] = "Celkem fragmentů složek",
        ["Free space count"] = "Počet oblastí volného místa",
        ["Average free space size"] = "Průměrná velikost volné oblasti",
        ["Largest free space size"] = "Největší souvislá volná oblast",
        ["MFT size"] = "Velikost MFT",
        ["MFT record count"] = "Počet záznamů MFT",
        ["MFT usage"] = "Využití MFT",
        ["Total MFT fragments"] = "Celkem fragmentů MFT",
    };

    /// <summary>Popisy fází z hlaviček „Stage N: ...".</summary>
    private static readonly Dictionary<string, string> StageDescriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Examining basic file system structure"] = "kontrola základní struktury systému souborů",
        ["Examining file name linkage"] = "kontrola provázání názvů souborů",
        ["Examining security descriptors"] = "kontrola deskriptorů zabezpečení",
        ["Looking for bad clusters in user file data"] = "hledání vadných clusterů v datech souborů",
        ["Looking for bad, free clusters"] = "hledání vadných volných clusterů",
        ["Examining Usn Journal"] = "kontrola žurnálu USN",
    };

    /// <summary>Názvy fází z řádků „Phase duration (...)".</summary>
    private static readonly Dictionary<string, string> PhaseNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["File record verification"] = "ověření záznamů souborů",
        ["Orphan file record recovery"] = "obnova osiřelých záznamů souborů",
        ["Bad file record checking"] = "kontrola vadných záznamů souborů",
        ["Index verification"] = "ověření indexů",
        ["Orphan reconnection"] = "znovupřipojení osiřelých souborů",
        ["Orphan recovery to lost and found"] = "obnova osiřelých souborů do složky lost and found",
        ["Reparse point and Object ID verification"] = "ověření reparse bodů a Object ID",
        ["Security descriptor verification"] = "ověření deskriptorů zabezpečení",
        ["Data attribute verification"] = "ověření datových atributů",
        ["Usn Journal verification"] = "ověření žurnálu USN",
    };

    /// <summary>Řádky „N něco processed." - jednotné schéma, liší se jen názvem počítané věci.</summary>
    private static readonly Dictionary<string, string> CountedItems = new(StringComparer.OrdinalIgnoreCase)
    {
        ["file records processed"] = "Zpracováno záznamů souborů",
        ["large file records processed"] = "Zpracováno velkých záznamů souborů",
        ["bad file records processed"] = "Zpracováno vadných záznamů souborů",
        ["reparse records processed"] = "Zpracováno reparse záznamů",
        ["index entries processed"] = "Zpracováno položek indexu",
        ["data files processed"] = "Zpracováno datových souborů",
        ["EA records processed"] = "Zpracováno záznamů rozšířených atributů",
        ["unindexed files scanned"] = "Prohledáno neindexovaných souborů",
        ["unindexed files recovered to lost and found"] = "Obnoveno neindexovaných souborů do složky lost and found",
        ["files processed"] = "Zpracováno souborů",
    };

    /// <summary>Souhrnné řádky o zabraném místě na konci výpisu chkdsk.</summary>
    private static readonly Dictionary<string, string> SpaceSummaries = new(StringComparer.OrdinalIgnoreCase)
    {
        ["total disk space"] = "Celková velikost disku",
        ["in bad sectors"] = "Ve vadných sektorech",
        ["in use by the system"] = "Využito systémem",
        ["occupied by the log file"] = "Zabírá soubor žurnálu",
        ["available on disk"] = "Volné místo na disku",
    };

    /// <summary>
    /// Řádky s průběžným postupem, kterých chkdsk vypíše stovky. Ve výpisu jsou
    /// k ničemu (postup ukazuje progress bar), jen by ho zaplavily - volající je
    /// tedy do zobrazení nepouští, ale pořád je používá k výpočtu procent.
    /// </summary>
    public static bool IsProgressNoise(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        if (ProgressRegex().IsMatch(line))
        {
            return true;
        }

        // Chkdsk „maže" předchozí řádek postupu tím, že vypíše řádek plný mezer.
        // Krátký prázdný řádek je naopak legitimní oddělovač odstavců, ten zůstává.
        return line.Length > PaddingLineLength && string.IsNullOrWhiteSpace(line);
    }

    private const int PaddingLineLength = 20;

    /// <summary>Přeloží řádek, nebo ho vrátí beze změny, když ho nezná.</summary>
    public static string Translate(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        var trimmed = line.Trim();
        if (trimmed.Length == 0)
        {
            return line;
        }

        var translated = TranslateCore(trimmed);
        if (translated is null)
        {
            return line;
        }

        // Odsazení nese strukturu reportu defragu, tak ho neztrácíme.
        var indent = line[..(line.Length - line.TrimStart().Length)];
        return indent + translated;
    }

    private static string? TranslateCore(string trimmed)
    {
        if (Phrases.TryGetValue(trimmed, out var phrase))
        {
            return phrase;
        }

        // Strojové značky z PowerShell orchestrace opravy (viz ChkdskRunner):
        // skript píše ASCII značku a českou větu skládá až tady, aby se do výstupu
        // nedostal zdroj skriptu ani text v cizím kódování.
        if (trimmed.StartsWith("DISKORA_STATUS:", StringComparison.Ordinal))
        {
            var status = trimmed["DISKORA_STATUS:".Length..].Trim();
            return status.Equals("NoErrorsFound", StringComparison.OrdinalIgnoreCase)
                ? "Oprava dokončena - žádné chyby k opravě nebyly nalezeny."
                : $"Stav opravy: {status}";
        }

        if (trimmed.StartsWith("DISKORA_ERROR:", StringComparison.Ordinal))
        {
            var reason = trimmed["DISKORA_ERROR:".Length..].Trim();
            return reason.Equals("NO_RESULT", StringComparison.Ordinal)
                ? "Oprava neproběhla: Repair-Volume nevrátil žádný výsledek. Nejčastěji chybí práva administrátora."
                : $"Oprava selhala: {reason}";
        }

        if (StageRegex().Match(trimmed) is { Success: true } stage)
        {
            var description = StageDescriptions.TryGetValue(stage.Groups["what"].Value.Trim(), out var mapped)
                ? mapped
                : stage.Groups["what"].Value.Trim();
            return $"Fáze {stage.Groups["number"].Value}: {description}...";
        }

        if (SimpleSentenceRegex().Match(trimmed) is { Success: true } sentence)
        {
            var value = sentence.Groups["value"].Value;
            return sentence.Groups["kind"].Value.ToUpperInvariant() switch
            {
                "THE TYPE OF THE FILE SYSTEM IS" => $"Souborový systém: {value}",
                "VOLUME LABEL IS" => $"Název svazku: {value}",
                _ => null,
            };
        }

        if (InvokingRegex().Match(trimmed) is { Success: true } invoking)
        {
            var target = invoking.Groups["target"].Value;
            return invoking.Groups["action"].Value.ToUpperInvariant() switch
            {
                "DEFRAGMENTATION" => $"Spouštím defragmentaci: {target}...",
                "ANALYSIS" => $"Spouštím analýzu: {target}...",
                "RETRIM" => $"Spouštím TRIM: {target}...",
                _ => null,
            };
        }

        if (CountedRegex().Match(trimmed) is { Success: true } counted
            && CountedItems.TryGetValue(counted.Groups["what"].Value, out var countedLabel))
        {
            return $"{countedLabel}: {counted.Groups["count"].Value}";
        }

        if (KbInRegex().Match(trimmed) is { Success: true } kbIn)
        {
            var what = kbIn.Groups["what"].Value.Equals("indexes", StringComparison.OrdinalIgnoreCase)
                ? "indexech"
                : "souborech";
            return $"V {what}: {kbIn.Groups["kb"].Value} KB ({kbIn.Groups["count"].Value})";
        }

        if (KbSummaryRegex().Match(trimmed) is { Success: true } kb
            && SpaceSummaries.TryGetValue(kb.Groups["what"].Value, out var kbLabel))
        {
            return $"{kbLabel}: {kb.Groups["kb"].Value} KB";
        }

        if (AllocationRegex().Match(trimmed) is { Success: true } allocation)
        {
            return allocation.Groups["what"].Value.ToUpperInvariant() switch
            {
                "BYTES IN EACH ALLOCATION UNIT" => $"Velikost alokační jednotky: {allocation.Groups["count"].Value} bajtů",
                "TOTAL ALLOCATION UNITS ON DISK" => $"Celkem alokačních jednotek: {allocation.Groups["count"].Value}",
                "ALLOCATION UNITS AVAILABLE ON DISK" => $"Volných alokačních jednotek: {allocation.Groups["count"].Value}",
                _ => null,
            };
        }

        if (PhaseDurationRegex().Match(trimmed) is { Success: true } phase)
        {
            var name = phase.Groups["name"].Value;
            var czechName = PhaseNames.TryGetValue(name, out var mapped) ? mapped : name;
            return $"Doba fáze ({czechName}): {TranslateDuration(phase.Groups["duration"].Value)}";
        }

        if (TotalDurationRegex().Match(trimmed) is { Success: true } total)
        {
            return $"Celková doba: {TranslateDuration(total.Groups["duration"].Value)}";
        }

        if (LabelValueRegex().Match(trimmed) is { Success: true } labelValue
            && Labels.TryGetValue(labelValue.Groups["label"].Value.Trim(), out var label))
        {
            return $"{label} = {TranslateDuration(labelValue.Groups["value"].Value)}";
        }

        return null;
    }

    /// <summary>Jednotky času a velikosti uvnitř jinak ponechané hodnoty.</summary>
    private static string TranslateDuration(string value) => UnitRegex().Replace(value, match => match.Value.ToUpperInvariant() switch
    {
        "MILLISECONDS" => "ms",
        "SECONDS" => "s",
        "MINUTES" => "min",
        "HOURS" => "h",
        "BYTES" => "bajtů",
        _ => match.Value,
    });

    [GeneratedRegex(@"^\s*Progress:\s.*done;", RegexOptions.IgnoreCase)]
    private static partial Regex ProgressRegex();

    [GeneratedRegex(@"^Stage\s+(?<number>\d+):\s*(?<what>.+?)\s*\.\.\.$", RegexOptions.IgnoreCase)]
    private static partial Regex StageRegex();

    [GeneratedRegex(@"^(?<kind>The type of the file system is|Volume label is)\s+(?<value>.+?)\.?$", RegexOptions.IgnoreCase)]
    private static partial Regex SimpleSentenceRegex();

    [GeneratedRegex(@"^Invoking\s+(?<action>\w+)\s+on\s+(?<target>.+?)\s*\.\.\.$", RegexOptions.IgnoreCase)]
    private static partial Regex InvokingRegex();

    [GeneratedRegex(@"^(?<count>[\d\s]+?)\s+(?<what>[A-Za-z][A-Za-z ]+?)\.$", RegexOptions.IgnoreCase)]
    private static partial Regex CountedRegex();

    [GeneratedRegex(@"^(?<kb>[\d\s]+?)\s+KB in\s+(?<count>\d+)\s+(?<what>files|indexes)\.$", RegexOptions.IgnoreCase)]
    private static partial Regex KbInRegex();

    [GeneratedRegex(@"^(?<kb>[\d\s]+?)\s+KB\s+(?<what>[A-Za-z][A-Za-z ]+?)\.$", RegexOptions.IgnoreCase)]
    private static partial Regex KbSummaryRegex();

    [GeneratedRegex(@"^(?<count>\d+)\s+(?<what>bytes in each allocation unit|total allocation units on disk|allocation units available on disk)\.$", RegexOptions.IgnoreCase)]
    private static partial Regex AllocationRegex();

    [GeneratedRegex(@"^Phase duration \((?<name>[^)]+)\):\s*(?<duration>.+?)\.?$", RegexOptions.IgnoreCase)]
    private static partial Regex PhaseDurationRegex();

    [GeneratedRegex(@"^Total duration:\s*(?<duration>.+?)\.?$", RegexOptions.IgnoreCase)]
    private static partial Regex TotalDurationRegex();

    [GeneratedRegex(@"^(?<label>[A-Za-z][A-Za-z ()/]+?)\s*=\s*(?<value>.+)$")]
    private static partial Regex LabelValueRegex();

    [GeneratedRegex(@"\b(milliseconds|seconds|minutes|hours|bytes)\b", RegexOptions.IgnoreCase)]
    private static partial Regex UnitRegex();
}

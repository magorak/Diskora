using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Diskora.Core.Formatting;
using Diskora.Core.Models;
using Diskora.Core.Services;
using Diskora.Core.Smart;
using Diskora.Data;
using Diskora.Repair;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var json = args.Any(a => a is "--json");
var positional = args.Where(a => a is not "--json").ToArray();

if (positional.Length == 0)
{
    PrintUsage();
    return 1;
}

var command = positional[0].ToLowerInvariant();
var rest = positional.Skip(1).ToArray();

try
{
    return command switch
    {
        "list" => RunList(json),
        "smart" => RunSmart(rest, json),
        "integrity" => await RunIntegrityAsync(rest, json, cts.Token),
        "usage" => await RunUsageAsync(rest, json, cts.Token),
        "duplicates" => await RunDuplicatesAsync(rest, json, cts.Token),
        "healthcheck" => RunHealthCheck(json),
        "schedule" => await RunScheduleAsync(rest, json, cts.Token),
        "help" or "-h" or "--help" => PrintUsage(),
        _ => PrintUnknownCommand(command),
    };
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Přerušeno.");
    return 130;
}

static int PrintUsage()
{
    Console.WriteLine("""
        Diskora CLI - headless společník ke GUI aplikaci Diskora.

        Použití: diskora <příkaz> [argumenty] [--json]

        Příkazy:
          list                          Seznam fyzických disků a svazků.
          smart <index-disku>           S.M.A.R.T. report daného fyzického disku.
          integrity <písmeno>[:] [--scan]
                                         Dirty-bit kontrola svazku; --scan navíc
                                         spustí needestruktivní chkdsk /scan
                                         (vyžaduje administrátorská práva).
          usage <cesta> [--top N]       Analýza zaplněnosti složky (výchozí top 10).
          duplicates <cesta>            Hledání duplicitních souborů (SHA-256).
          healthcheck                   S.M.A.R.T. přes všechny fyzické disky najednou
                                         (pro naplánovanou kontrolu, viz „schedule").
          schedule install [--time HH:mm]
                                         Naplánuje denní „healthcheck" v Plánovači úloh
                                         Windows (výchozí čas 09:00).
          schedule remove               Zruší naplánovanou kontrolu.
          schedule status               Zobrazí, jestli je kontrola naplánovaná.

        Přepínač --json vypíše výstup jako JSON místo tabulky pro člověka
        (skriptování/automatizace). Bez admin práv fungují list/usage/duplicates,
        dirty-bit část integrity a schedule (běží pod aktuálním uživatelem); smart,
        healthcheck a integrity --scan podle disku/svazku mohou vyžadovat elevaci.
        """);
    return 0;
}

static int PrintUnknownCommand(string command)
{
    Console.Error.WriteLine($"Neznámý příkaz „{command}“. Spusťte „diskora help“ pro nápovědu.");
    return 1;
}

static int RunList(bool json)
{
    var service = new DiskEnumerationService();
    var disks = service.GetPhysicalDisks();
    var volumes = service.GetVolumes();

    if (json)
    {
        PrintJson(new { PhysicalDisks = disks, Volumes = volumes });
        return 0;
    }

    Console.WriteLine("Fyzické disky:");
    Console.WriteLine($"{"#",-4} {"Název",-32} {"Velikost",-10} {"Typ",-8} {"Rozhraní",-10}");
    foreach (var disk in disks)
    {
        Console.WriteLine(
            $"{disk.Index,-4} {Truncate(disk.FriendlyName, 32),-32} {ByteSizeFormatter.Format((long)disk.SizeBytes),-10} {disk.MediaType,-8} {disk.BusType,-10}");
    }

    Console.WriteLine();
    Console.WriteLine("Svazky:");
    Console.WriteLine($"{"Písmeno",-9} {"Název",-20} {"Systém",-8} {"Celkem",-10} {"Volno",-10}");
    foreach (var volume in volumes)
    {
        Console.WriteLine(
            $"{volume.Name,-9} {Truncate(volume.Label ?? "—", 20),-20} {volume.FileSystem ?? "—",-8} {ByteSizeFormatter.Format(volume.TotalSizeBytes),-10} {ByteSizeFormatter.Format(volume.FreeSpaceBytes),-10}");
    }

    return 0;
}

static int RunSmart(string[] rest, bool json)
{
    if (rest.Length < 1 || !int.TryParse(rest[0], out var diskIndex))
    {
        Console.Error.WriteLine("Použití: diskora smart <index-disku> [--json]");
        return 1;
    }

    var service = new SmartService(new SqliteDiskHistoryStore());
    var result = service.ReadReport(diskIndex);

    if (json)
    {
        PrintJson(result);
        return result.IsSupported ? 0 : 2;
    }

    if (!result.IsSupported || result.Report is null)
    {
        Console.WriteLine($"S.M.A.R.T. není pro disk {diskIndex} dostupné: {result.UnavailableReason}");
        return 2;
    }

    var report = result.Report;
    Console.WriteLine($"Disk {diskIndex} - celkový stav: {report.OverallHealth}");
    Console.WriteLine($"{"ID",-4} {"Název",-38} {"Aktuální",-9} {"Nejhorší",-9} {"Práh",-6} {"Surová hodnota",-15} {"Riziko",-8}");
    foreach (var attribute in report.Attributes)
    {
        var risk = SmartHealthEvaluator.EvaluateAttributeRisk(attribute);
        Console.WriteLine(
            $"{attribute.Id,-4} {Truncate(SmartAttributeCatalog.GetName(attribute.Id), 38),-38} {attribute.CurrentValue,-9} {attribute.WorstValue,-9} {attribute.Threshold,-6} {attribute.RawValue,-15} {risk,-8}");
    }

    return 0;
}

static int RunHealthCheck(bool json)
{
    var enumerationService = new DiskEnumerationService();
    var smartService = new SmartService(new SqliteDiskHistoryStore());

    var rows = enumerationService.GetPhysicalDisks()
        .Select(disk => (Disk: disk, Result: smartService.ReadReport(disk.Index)))
        .ToList();

    var hasProblem = rows.Any(r => !r.Result.IsSupported
        || r.Result.Report?.OverallHealth is DiskHealthStatus.Warning or DiskHealthStatus.Critical);

    if (json)
    {
        PrintJson(rows.Select(r => new
        {
            r.Disk.Index,
            r.Disk.FriendlyName,
            r.Result.IsSupported,
            OverallHealth = r.Result.Report?.OverallHealth,
            r.Result.UnavailableReason,
        }));
        return hasProblem ? 2 : 0;
    }

    Console.WriteLine($"{"#",-4} {"Disk",-32} {"Stav",-12}");
    foreach (var (disk, result) in rows)
    {
        var status = result.IsSupported ? result.Report!.OverallHealth.ToString() : "nedostupné";
        Console.WriteLine($"{disk.Index,-4} {Truncate(disk.FriendlyName, 32),-32} {status,-12}");
    }

    return hasProblem ? 2 : 0;
}

static async Task<int> RunScheduleAsync(string[] rest, bool json, CancellationToken cancellationToken)
{
    if (rest.Length < 1)
    {
        Console.Error.WriteLine("Použití: diskora schedule <install|remove|status> [--time HH:mm] [--json]");
        return 1;
    }

    var action = rest[0].ToLowerInvariant();
    ScheduledTaskResult result;

    switch (action)
    {
        case "install":
            var time = "09:00";
            var timeIndex = Array.IndexOf(rest, "--time");
            if (timeIndex >= 0 && timeIndex + 1 < rest.Length)
            {
                time = rest[timeIndex + 1];
            }

            var executablePath = Environment.ProcessPath
                ?? throw new InvalidOperationException("Cestu k vlastnímu spustitelnému souboru se nepodařilo zjistit.");
            result = await ScheduledTaskManager.InstallAsync(executablePath, time, cancellationToken);
            break;
        case "remove":
            result = await ScheduledTaskManager.RemoveAsync(cancellationToken);
            break;
        case "status":
            result = await ScheduledTaskManager.QueryAsync(cancellationToken);
            break;
        default:
            Console.Error.WriteLine($"Neznámá akce „{action}“. Použijte install, remove nebo status.");
            return 1;
    }

    if (json)
    {
        PrintJson(result);
        return result.Started && result.ExitCode == 0 ? 0 : (result.Started ? 2 : 1);
    }

    foreach (var line in result.OutputLines)
    {
        Console.WriteLine(line);
    }

    if (!result.Started)
    {
        Console.Error.WriteLine(result.FailureReason);
        return 1;
    }

    return result.ExitCode == 0 ? 0 : 2;
}

static async Task<int> RunIntegrityAsync(string[] rest, bool json, CancellationToken cancellationToken)
{
    var scan = rest.Any(a => a is "--scan");
    var positionalRest = rest.Where(a => a is not "--scan").ToArray();

    if (positionalRest.Length < 1)
    {
        Console.Error.WriteLine("Použití: diskora integrity <písmeno>[:] [--scan] [--json]");
        return 1;
    }

    var driveLetter = NormalizeDriveLetter(positionalRest[0]);
    var service = new IntegrityCheckService(new SqliteDiskHistoryStore());
    var dirtyState = service.CheckDirtyState(driveLetter);

    IntegrityScanOutcome? scanOutcome = null;
    if (scan)
    {
        var progress = json ? null : new Progress<string>(Console.WriteLine);
        scanOutcome = await service.RunReadOnlyScanAsync(driveLetter, progress, cancellationToken);
    }

    if (json)
    {
        PrintJson(new { DriveLetter = driveLetter, DirtyState = dirtyState, Scan = scanOutcome });
        return dirtyState == VolumeDirtyState.Dirty ? 2 : 0;
    }

    Console.WriteLine($"{driveLetter} - stav: {dirtyState}");
    if (scanOutcome is not null)
    {
        Console.WriteLine(scanOutcome.Started
            ? $"chkdsk /scan dokončen, kód {scanOutcome.ExitCode} ({(scanOutcome.AppearsClean ? "v pořádku" : "nalezeny problémy")})"
            : $"chkdsk /scan se nespustil: {scanOutcome.FailureReason}");
    }

    return dirtyState == VolumeDirtyState.Dirty ? 2 : 0;
}

static async Task<int> RunUsageAsync(string[] rest, bool json, CancellationToken cancellationToken)
{
    if (rest.Length < 1)
    {
        Console.Error.WriteLine("Použití: diskora usage <cesta> [--top N] [--json]");
        return 1;
    }

    var path = rest[0];
    var top = 10;
    var topIndex = Array.IndexOf(rest, "--top");
    if (topIndex >= 0 && topIndex + 1 < rest.Length && int.TryParse(rest[topIndex + 1], out var parsedTop))
    {
        top = Math.Max(1, parsedTop);
    }

    if (!Directory.Exists(path))
    {
        Console.Error.WriteLine($"Složka „{path}“ neexistuje.");
        return 1;
    }

    var progress = json ? null : new Progress<string>(p => Console.Error.Write($"\rProhledávám: {Truncate(p, 100)}   "));
    var scanner = new DiskUsageScanner();
    var result = await scanner.ScanAsync(path, progress, cancellationToken);
    if (!json)
    {
        Console.Error.WriteLine();
    }

    var largest = result.LargestFiles.Take(top).ToList();
    var oldest = result.OldestFiles.Take(top).ToList();

    if (json)
    {
        PrintJson(new
        {
            RootPath = path,
            result.Root.SizeBytes,
            result.Root.FileCount,
            SubdirectoryCount = result.Root.Subdirectories.Count,
            LargestFiles = largest,
            OldestFiles = oldest,
        });
        return 0;
    }

    Console.WriteLine($"{path}: {ByteSizeFormatter.Format(result.Root.SizeBytes)} celkem, {result.Root.FileCount} souborů, {result.Root.Subdirectories.Count} podsložek");

    Console.WriteLine();
    Console.WriteLine($"Top {top} podsložek podle velikosti:");
    foreach (var sub in result.Root.Subdirectories.OrderByDescending(s => s.SizeBytes).Take(top))
    {
        Console.WriteLine($"  {ByteSizeFormatter.Format(sub.SizeBytes),-10} {sub.Name}");
    }

    Console.WriteLine();
    Console.WriteLine($"Top {top} největších souborů:");
    foreach (var file in largest)
    {
        Console.WriteLine($"  {ByteSizeFormatter.Format(file.SizeBytes),-10} {file.FullPath}");
    }

    Console.WriteLine();
    Console.WriteLine($"Top {top} nejstarších souborů:");
    foreach (var file in oldest)
    {
        Console.WriteLine($"  {file.LastWriteTimeUtc.ToLocalTime(),-20:dd.MM.yyyy HH:mm} {file.FullPath}");
    }

    return 0;
}

static async Task<int> RunDuplicatesAsync(string[] rest, bool json, CancellationToken cancellationToken)
{
    if (rest.Length < 1)
    {
        Console.Error.WriteLine("Použití: diskora duplicates <cesta> [--json]");
        return 1;
    }

    var path = rest[0];
    if (!Directory.Exists(path))
    {
        Console.Error.WriteLine($"Složka „{path}“ neexistuje.");
        return 1;
    }

    var progress = json ? null : new Progress<string>(p => Console.Error.Write($"\rProhledávám: {Truncate(p, 100)}   "));
    var finder = new DuplicateFileFinder();
    var groups = await finder.FindAsync(path, progress, cancellationToken);
    if (!json)
    {
        Console.Error.WriteLine();
    }

    if (json)
    {
        PrintJson(groups);
        return groups.Count > 0 ? 2 : 0;
    }

    if (groups.Count == 0)
    {
        Console.WriteLine("Žádné duplicitní soubory nenalezeny.");
        return 0;
    }

    var totalReclaimable = groups.Sum(g => g.ReclaimableBytes);
    Console.WriteLine($"{groups.Count} skupin duplicit, možná úspora {ByteSizeFormatter.Format(totalReclaimable)}.");
    Console.WriteLine();

    var groupNumber = 1;
    foreach (var group in groups)
    {
        Console.WriteLine($"Skupina {groupNumber} - {ByteSizeFormatter.Format(group.SizeBytes)} x {group.FilePaths.Count}:");
        foreach (var filePath in group.FilePaths)
        {
            Console.WriteLine($"  {filePath}");
        }

        groupNumber++;
    }

    return 2;
}

static string NormalizeDriveLetter(string input)
{
    var trimmed = input.TrimEnd('\\');
    return trimmed.EndsWith(':') ? trimmed : trimmed + ":";
}

static string Truncate(string value, int maxLength) =>
    value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength - 1), "…");

static void PrintJson<T>(T value)
{
    var options = new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Latin1Supplement, UnicodeRanges.LatinExtendedA),
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };
    Console.WriteLine(JsonSerializer.Serialize(value, options));
}

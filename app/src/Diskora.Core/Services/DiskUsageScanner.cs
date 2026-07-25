using Diskora.Core.Models;

namespace Diskora.Core.Services;

/// <summary>
/// Rekurzivně spočítá velikost každé složky ve stromu (styl TreeSize) a
/// zároveň sleduje TopFilesCount největších a nejstarších souborů - přes
/// bounded tracker, ne přes seznam všech souborů, aby paměť neškálovala
/// s počtem souborů na velkých discích.
///
/// Sourozenecké podsložky se skenují souběžně (ThreadPool přes Task.Run),
/// ale semafor <see cref="MaxConcurrency"/> omezuje jen samotné I/O jedné
/// složky (výpis adresáře + čtení metadat jejích souborů) - permit se drží
/// jen po dobu tohoto krátkého synchronního úseku a uvolní se PŘED rekurzí
/// do potomků. Živě zjištěno (empiricky, na reálném C:\ svazku), že držet
/// permit i během čekání na potomky vede k prioritní inverzi: rodič drží
/// permit a čeká na potomka, který ale potřebuje permit ze stejné fronty -
/// hluboké větve se tak umí zaseknout na řádově minuty navíc, hůř než bez
/// jakéhokoli omezení souběžnosti.
///
/// Hlášení postupu (<see cref="ThrottledProgressReporter"/>) je prahováno na
/// max. 10x/s - živě zjištěno, že bez prahování hlášení KAŽDÉ navštívené
/// složky (statisíce na velkém svazku) zaplaví UI vlákno rychleji, než
/// stihne odbavovat frontu, takže sken vypadá zaseknutý i dlouho po
/// dokončení skutečné I/O práce.
///
/// Nepřístupné složky/soubory se přeskočí a označí, místo aby shodily
/// celý sken - dílčí nedostupnost je běžný stav (systémové/cizí profily),
/// ne důvod k pádu. Reparse pointy (junction/symlink) se nerozbalují, aby
/// nevznikla nekonečná smyčka nebo dvojí započítání velikosti.
/// </summary>
public sealed class DiskUsageScanner : IDiskUsageScanner
{
    private const int TopFilesCount = 20;

    private static readonly int MaxConcurrency = Math.Max(2, Environment.ProcessorCount * 2);

    private static readonly TimeSpan ProgressReportInterval = TimeSpan.FromMilliseconds(100);

    public async Task<DiskUsageScanResult> ScanAsync(
        string rootPath,
        IProgress<string>? onDirectoryScanned = null,
        CancellationToken cancellationToken = default)
    {
        var largestTracker = new BoundedTopTracker(TopFilesCount, (a, b) => b.SizeBytes.CompareTo(a.SizeBytes));
        var oldestTracker = new BoundedTopTracker(TopFilesCount, (a, b) => a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc));
        var reporter = new ThrottledProgressReporter(onDirectoryScanned, ProgressReportInterval);

        using var ioLimiter = new SemaphoreSlim(MaxConcurrency);

        var root = await ScanDirectoryAsync(rootPath, reporter, cancellationToken, largestTracker, oldestTracker, ioLimiter)
            .ConfigureAwait(false);

        return new DiskUsageScanResult(root, largestTracker.ToList(), oldestTracker.ToList());
    }

    private static async Task<DirectoryUsageNode> ScanDirectoryAsync(
        string path,
        ThrottledProgressReporter reporter,
        CancellationToken cancellationToken,
        BoundedTopTracker largestTracker,
        BoundedTopTracker oldestTracker,
        SemaphoreSlim ioLimiter)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Permit se drží jen pro tento synchronní blok (výpis + metadata
        // souborů téhle jedné složky) a explicitně uvolní PŘED rekurzí do
        // potomků o pár řádků níž - viz vysvětlení u třídy.
        await ioLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);
        DirectoryUsageNode node;
        string[] subdirectories;
        try
        {
            (node, subdirectories) = await Task.Run(
                () => ScanOwnFiles(path, reporter, cancellationToken, largestTracker, oldestTracker),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ioLimiter.Release();
        }

        if (node.HadAccessError || subdirectories.Length == 0)
        {
            return node;
        }

        var childTasks = new List<Task<DirectoryUsageNode>>(subdirectories.Length);
        foreach (var directory in subdirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsReparsePoint(directory))
            {
                childTasks.Add(Task.FromResult(new DirectoryUsageNode
                {
                    Name = Path.GetFileName(directory),
                    FullPath = directory,
                    IsReparsePoint = true,
                }));
                continue;
            }

            childTasks.Add(ScanDirectoryAsync(directory, reporter, cancellationToken, largestTracker, oldestTracker, ioLimiter));
        }

        var children = await Task.WhenAll(childTasks).ConfigureAwait(false);

        long size = node.SizeBytes;
        var fileCount = node.FileCount;
        foreach (var child in children)
        {
            node.Subdirectories.Add(child);
            size += child.SizeBytes;
            fileCount += child.FileCount;
        }

        node.SizeBytes = size;
        node.FileCount = fileCount;
        return node;
    }

    /// <summary>
    /// Čistě synchronní část skenu jedné složky: výpis obsahu a přečtení
    /// metadat jejích přímých souborů. Volá se pod <see cref="SemaphoreSlim"/>
    /// z <see cref="ScanDirectoryAsync"/> - žádná rekurze, žádné čekání na
    /// potomky, takže permit se drží jen na dobu skutečné I/O práce.
    /// </summary>
    private static (DirectoryUsageNode Node, string[] Subdirectories) ScanOwnFiles(
        string path,
        ThrottledProgressReporter reporter,
        CancellationToken cancellationToken,
        BoundedTopTracker largestTracker,
        BoundedTopTracker oldestTracker)
    {
        reporter.Report(path);

        var name = Path.GetFileName(path);
        var node = new DirectoryUsageNode
        {
            Name = string.IsNullOrEmpty(name) ? path : name,
            FullPath = path,
        };

        string[] subdirectories;
        string[] files;
        try
        {
            subdirectories = Directory.GetDirectories(path);
            files = Directory.GetFiles(path);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            node.HadAccessError = true;
            return (node, []);
        }

        long size = 0;
        var fileCount = 0;

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var info = new FileInfo(file);
                size += info.Length;
                fileCount++;

                var entry = new FileUsageEntry(info.FullName, info.Name, info.Length, info.LastWriteTimeUtc);
                largestTracker.Consider(entry);
                oldestTracker.Consider(entry);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                // Soubor zmizel nebo je nedostupný mezi výpisem a čtením - přeskočí se.
            }
        }

        node.SizeBytes = size;
        node.FileCount = fileCount;
        return (node, subdirectories);
    }

    private static bool IsReparsePoint(string directoryPath)
    {
        try
        {
            return new DirectoryInfo(directoryPath).Attributes.HasFlag(FileAttributes.ReparsePoint);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    /// <summary>
    /// Udržuje nejvýše <paramref name="capacity"/> položek seřazených podle
    /// <paramref name="comparison"/> (nejlepší první) - použito jednou pro
    /// "největší podle velikosti sestupně" a jednou pro "nejstarší podle
    /// data poslední změny vzestupně", bez alokace pro každý navštívený soubor.
    /// Thread-safe (<see cref="Consider"/> se volá souběžně z více souběžně
    /// skenovaných větví stromu).
    /// </summary>
    private sealed class BoundedTopTracker(int capacity, Comparison<FileUsageEntry> comparison)
    {
        private readonly List<FileUsageEntry> _items = [];
        private readonly IComparer<FileUsageEntry> _comparer = Comparer<FileUsageEntry>.Create(comparison);
        private readonly object _lock = new();

        public void Consider(FileUsageEntry entry)
        {
            lock (_lock)
            {
                var index = _items.BinarySearch(entry, _comparer);
                if (index < 0)
                {
                    index = ~index;
                }

                if (index >= capacity)
                {
                    return;
                }

                _items.Insert(index, entry);
                if (_items.Count > capacity)
                {
                    _items.RemoveAt(_items.Count - 1);
                }
            }
        }

        public IReadOnlyList<FileUsageEntry> ToList()
        {
            lock (_lock)
            {
                return _items.ToList();
            }
        }
    }

    /// <summary>
    /// Prahuje volání <see cref="IProgress{T}.Report"/> na nejvýše jednou za
    /// <paramref name="minInterval"/> - živě zjištěno, že hlásit KAŽDOU
    /// navštívenou složku (statisíce na velkém svazku) zaplaví UI vlákno
    /// (každé volání skrz WPF binding spouští <c>CommandManager.
    /// InvalidateRequerySuggested()</c>, což je drahá globální operace) natolik,
    /// že samotné dokreslení průběhu trvá o řády déle než skutečný sken.
    /// Thread-safe přes <see cref="Interlocked.CompareExchange(ref long, long, long)"/>,
    /// aby souběžně skenované větve o throttling okno nesoutěžily nekorektně.
    /// </summary>
    private sealed class ThrottledProgressReporter(IProgress<string>? inner, TimeSpan minInterval)
    {
        // Nastaveno tak, aby PRVNÍ volání Report vždy prošlo bez zvláštního
        // případu - "long.MinValue" by při "now - last" přetekl (now je vždy
        // kladné, ale rozdíl vůči long.MinValue přesahuje long.MaxValue a
        // wrapne se na zápornou hodnotu), takže by throttling tiše zahazoval
        // úplně všechna hlášení navždy - živě odhaleno testem, který čekal
        // aspoň jedno zaznamenané hlášení a dostal prázdnou kolekci.
        private long _lastReportTicks = Environment.TickCount64 - (long)minInterval.TotalMilliseconds - 1;

        public void Report(string path)
        {
            if (inner is null)
            {
                return;
            }

            var now = Environment.TickCount64;
            var last = Interlocked.Read(ref _lastReportTicks);
            if (now - last < minInterval.TotalMilliseconds)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _lastReportTicks, now, last) == last)
            {
                inner.Report(path);
            }
        }
    }
}

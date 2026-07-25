using Diskora.Core.Models;

namespace Diskora.Core.Services;

/// <summary>
/// Rekurzivně spočítá velikost každé složky ve stromu (styl TreeSize) a
/// zároveň sleduje TopFilesCount největších a nejstarších souborů - přes
/// bounded tracker, ne přes seznam všech souborů, aby paměť neškálovala
/// s počtem souborů na velkých discích.
/// Nepřístupné složky/soubory se přeskočí a označí, místo aby shodily
/// celý sken - dílčí nedostupnost je běžný stav (systémové/cizí profily),
/// ne důvod k pádu. Reparse pointy (junction/symlink) se nerozbalují, aby
/// nevznikla nekonečná smyčka nebo dvojí započítání velikosti.
/// </summary>
public sealed class DiskUsageScanner : IDiskUsageScanner
{
    private const int TopFilesCount = 20;

    public Task<DiskUsageScanResult> ScanAsync(
        string rootPath,
        IProgress<string>? onDirectoryScanned = null,
        CancellationToken cancellationToken = default) =>
        Task.Run(
            () =>
            {
                var largestTracker = new BoundedTopTracker(TopFilesCount, (a, b) => b.SizeBytes.CompareTo(a.SizeBytes));
                var oldestTracker = new BoundedTopTracker(TopFilesCount, (a, b) => a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc));

                var root = ScanDirectory(rootPath, onDirectoryScanned, cancellationToken, largestTracker, oldestTracker);

                return new DiskUsageScanResult(root, largestTracker.ToList(), oldestTracker.ToList());
            },
            cancellationToken);

    private static DirectoryUsageNode ScanDirectory(
        string path,
        IProgress<string>? onDirectoryScanned,
        CancellationToken cancellationToken,
        BoundedTopTracker largestTracker,
        BoundedTopTracker oldestTracker)
    {
        cancellationToken.ThrowIfCancellationRequested();
        onDirectoryScanned?.Report(path);

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
            return node;
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

        foreach (var directory in subdirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsReparsePoint(directory))
            {
                node.Subdirectories.Add(new DirectoryUsageNode
                {
                    Name = Path.GetFileName(directory),
                    FullPath = directory,
                    IsReparsePoint = true,
                });
                continue;
            }

            var child = ScanDirectory(directory, onDirectoryScanned, cancellationToken, largestTracker, oldestTracker);
            node.Subdirectories.Add(child);
            size += child.SizeBytes;
            fileCount += child.FileCount;
        }

        node.SizeBytes = size;
        node.FileCount = fileCount;
        return node;
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
    /// </summary>
    private sealed class BoundedTopTracker(int capacity, Comparison<FileUsageEntry> comparison)
    {
        private readonly List<FileUsageEntry> _items = [];
        private readonly IComparer<FileUsageEntry> _comparer = Comparer<FileUsageEntry>.Create(comparison);

        public void Consider(FileUsageEntry entry)
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

        public IReadOnlyList<FileUsageEntry> ToList() => _items;
    }
}

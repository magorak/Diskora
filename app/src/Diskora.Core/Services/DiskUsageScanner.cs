using Diskora.Core.Models;

namespace Diskora.Core.Services;

/// <summary>
/// Rekurzivně spočítá velikost každé složky ve stromu (styl TreeSize).
/// Nepřístupné složky/soubory se přeskočí a označí, místo aby shodily
/// celý sken - dílčí nedostupnost je běžný stav (systémové/cizí profily),
/// ne důvod k pádu. Reparse pointy (junction/symlink) se nerozbalují, aby
/// nevznikla nekonečná smyčka nebo dvojí započítání velikosti.
/// </summary>
public sealed class DiskUsageScanner : IDiskUsageScanner
{
    public Task<DirectoryUsageNode> ScanAsync(
        string rootPath,
        IProgress<string>? onDirectoryScanned = null,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => ScanDirectory(rootPath, onDirectoryScanned, cancellationToken), cancellationToken);

    private static DirectoryUsageNode ScanDirectory(
        string path,
        IProgress<string>? onDirectoryScanned,
        CancellationToken cancellationToken)
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
                size += new FileInfo(file).Length;
                fileCount++;
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

            var child = ScanDirectory(directory, onDirectoryScanned, cancellationToken);
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
}

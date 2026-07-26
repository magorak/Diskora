using System.Collections.Concurrent;
using System.Security.Cryptography;
using Diskora.Core.Models;

namespace Diskora.Core.Services;

/// <summary>
/// Najde soubory se shodným obsahem pod danou složkou. Dvoufázový přístup:
/// (1) jednovláknově projde strom a sesbírá (cesta, velikost) - velikost je
/// zadarmo z metadat a okamžitě vyřadí drtivou většinu souborů (nemají shodnou
/// velikost s ničím jiným, tedy nemůžou být duplicitní); (2) teprve soubory se
/// shodnou velikostí se hashují (SHA-256, paralelně přes <see cref="Parallel.ForEachAsync"/>),
/// protože hashování je to skutečně drahé (čte celý obsah souboru).
///
/// Procházení stromu je záměrně JEDNOVLÁKNOVÉ, ne paralelní jako
/// <see cref="DiskUsageScanner"/> - u té paralelizace se cestou objevily dvě
/// netriviální souběžnostní chyby (prioritní inverze v semaforu, zaplavení UI
/// vlákna hlášením postupu) a zde by přinesla jen malý zisk, protože skutečné
/// I/O těžiště (hashování) je paralelizované samostatně a bezpečně (plochý
/// seznam, žádná rekurze, žádné sdílené permity mezi rodičem a potomkem).
///
/// Read-only diagnostika - nic se nemaže ani nepřejmenovává. Skutečné čištění
/// duplicit by potřebovalo vlastní potvrzovací UI (stejně jako `chkdsk /f`).
/// </summary>
public sealed class DuplicateFileFinder : IDuplicateFileFinder
{
    private static readonly int MaxConcurrency = Math.Max(2, Environment.ProcessorCount);
    private static readonly TimeSpan ProgressReportInterval = TimeSpan.FromMilliseconds(100);

    public async Task<IReadOnlyList<DuplicateFileGroup>> FindAsync(
        string rootPath,
        IProgress<string>? onFileScanned = null,
        CancellationToken cancellationToken = default)
    {
        var files = new List<(string Path, long Size)>();
        var lastReportTicks = Environment.TickCount64 - (long)ProgressReportInterval.TotalMilliseconds - 1;

        void ReportThrottled(string path)
        {
            if (onFileScanned is null)
            {
                return;
            }

            var now = Environment.TickCount64;
            if (now - lastReportTicks < ProgressReportInterval.TotalMilliseconds)
            {
                return;
            }

            lastReportTicks = now;
            onFileScanned.Report(path);
        }

        void WalkDirectory(string path)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReportThrottled(path);

            string[] subdirectories;
            string[] localFiles;
            try
            {
                subdirectories = Directory.GetDirectories(path);
                localFiles = Directory.GetFiles(path);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                return;
            }

            foreach (var file in localFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var info = new FileInfo(file);
                    if (info.Length > 0)
                    {
                        files.Add((info.FullName, info.Length));
                    }
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    // Soubor zmizel nebo je nedostupný mezi výpisem a čtením - přeskočí se.
                }
            }

            foreach (var directory in subdirectories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsReparsePoint(directory))
                {
                    WalkDirectory(directory);
                }
            }
        }

        await Task.Run(() => WalkDirectory(rootPath), cancellationToken).ConfigureAwait(false);

        var candidates = files
            .GroupBy(f => f.Size)
            .Where(g => g.Count() > 1)
            .SelectMany(g => g)
            .ToList();

        var hashed = new ConcurrentBag<(long Size, string Hash, string Path)>();

        await Parallel.ForEachAsync(
            candidates,
            new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrency, CancellationToken = cancellationToken },
            async (file, ct) =>
            {
                var hash = await TryComputeHashAsync(file.Path, ct).ConfigureAwait(false);
                if (hash is not null)
                {
                    hashed.Add((file.Size, hash, file.Path));
                }
            }).ConfigureAwait(false);

        return hashed
            .GroupBy(r => (r.Size, r.Hash))
            .Where(g => g.Count() > 1)
            .Select(g => new DuplicateFileGroup(
                g.Key.Size,
                g.Select(x => x.Path).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList()))
            .OrderByDescending(g => g.ReclaimableBytes)
            .ToList();
    }

    private static async Task<string?> TryComputeHashAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1024 * 1024, useAsync: true);
            var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
            return Convert.ToHexStringLower(hashBytes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Soubor zmizel nebo je zamčený jiným procesem mezi výpisem a
            // hashováním - přeskočí se, nezastaví to zbytek hledání.
            return null;
        }
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

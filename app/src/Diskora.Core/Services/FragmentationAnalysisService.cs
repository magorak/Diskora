using System.Collections.Concurrent;
using Diskora.Core.Models;
using Diskora.Native.Storage;

namespace Diskora.Core.Services;

/// <summary>
/// Read-only report fragmentace souborů na svazku/ve složce - needestruktivní
/// podklad pro rozhodnutí, jestli má smysl spustit defragmentaci (Fáze 5).
/// Prochází strom jednovláknově (stejný důvod jako <see cref="DuplicateFileFinder"/> -
/// u paralelizace stromu ve <see cref="DiskUsageScanner"/> se objevily dvě netriviální
/// souběžnostní chyby), samotné čtení rozvržení každého souboru
/// (<see cref="FileFragmentationReader"/>) je paralelizované přes
/// <see cref="Parallel.ForEachAsync{TSource}(IAsyncEnumerable{TSource}, Func{TSource, CancellationToken, ValueTask})"/>.
/// </summary>
public sealed class FragmentationAnalysisService : IFragmentationAnalysisService
{
    private static readonly int MaxConcurrency = Math.Max(2, Environment.ProcessorCount);
    private static readonly TimeSpan ProgressReportInterval = TimeSpan.FromMilliseconds(100);
    private const int TopFilesCount = 20;

    public async Task<FragmentationAnalysisResult> AnalyzeAsync(
        string rootPath, IProgress<string>? onFileScanned = null, CancellationToken cancellationToken = default)
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

        var fragmentedFiles = new ConcurrentBag<FragmentedFileEntry>();

        await Parallel.ForEachAsync(
            files,
            new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrency, CancellationToken = cancellationToken },
            async (file, ct) =>
            {
                var result = await Task.Run(() => FileFragmentationReader.GetExtentCount(file.Path), ct).ConfigureAwait(false);
                if (result.Success && result.ExtentCount > 1)
                {
                    fragmentedFiles.Add(new FragmentedFileEntry(file.Path, file.Size, result.ExtentCount, result.ExtentCountIsLowerBound));
                }
            }).ConfigureAwait(false);

        var topFragmented = fragmentedFiles
            .OrderByDescending(f => f.FragmentCount)
            .ThenByDescending(f => f.SizeBytes)
            .Take(TopFilesCount)
            .ToList();

        return new FragmentationAnalysisResult(files.Count, fragmentedFiles.Count, topFragmented);
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

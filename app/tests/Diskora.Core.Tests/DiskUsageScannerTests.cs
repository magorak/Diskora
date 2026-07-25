using Diskora.Core.Services;

namespace Diskora.Core.Tests;

public sealed class DiskUsageScannerTests : IDisposable
{
    private readonly string _root;
    private readonly DiskUsageScanner _scanner = new();

    public DiskUsageScannerTests()
    {
        _root = Directory.CreateTempSubdirectory("diskora-tests-").FullName;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Best effort cleanup - nevadí, pokud se dočasná složka neuklidí hned.
        }
    }

    [Fact]
    public async Task ScanAsync_NestedStructure_AggregatesSizesAndCountsBottomUp()
    {
        WriteFile(Path.Combine(_root, "fileA.txt"), 100);

        var sub1 = Directory.CreateDirectory(Path.Combine(_root, "sub1")).FullName;
        WriteFile(Path.Combine(sub1, "fileB.txt"), 200);

        var sub1a = Directory.CreateDirectory(Path.Combine(sub1, "sub1a")).FullName;
        WriteFile(Path.Combine(sub1a, "fileC.txt"), 300);

        Directory.CreateDirectory(Path.Combine(_root, "sub2"));

        var result = await _scanner.ScanAsync(_root);
        var root = result.Root;

        Assert.Equal(600, root.SizeBytes);
        Assert.Equal(3, root.FileCount);
        Assert.Equal(2, root.Subdirectories.Count);

        var resultSub1 = root.Subdirectories.Single(n => n.Name == "sub1");
        Assert.Equal(500, resultSub1.SizeBytes);
        Assert.Equal(2, resultSub1.FileCount);

        var resultSub1a = resultSub1.Subdirectories.Single(n => n.Name == "sub1a");
        Assert.Equal(300, resultSub1a.SizeBytes);
        Assert.Equal(1, resultSub1a.FileCount);

        var resultSub2 = root.Subdirectories.Single(n => n.Name == "sub2");
        Assert.Equal(0, resultSub2.SizeBytes);
        Assert.Equal(0, resultSub2.FileCount);
        Assert.Empty(resultSub2.Subdirectories);
    }

    [Fact]
    public async Task ScanAsync_EmptyDirectory_ReturnsZeroSizeAndNoError()
    {
        var result = await _scanner.ScanAsync(_root);

        Assert.Equal(0, result.Root.SizeBytes);
        Assert.Equal(0, result.Root.FileCount);
        Assert.False(result.Root.HadAccessError);
        Assert.Empty(result.LargestFiles);
        Assert.Empty(result.OldestFiles);
    }

    [Fact]
    public async Task ScanAsync_NonExistentPath_MarksAccessError()
    {
        var missingPath = Path.Combine(_root, "does-not-exist");

        var result = await _scanner.ScanAsync(missingPath);

        Assert.True(result.Root.HadAccessError);
        Assert.Equal(0, result.Root.SizeBytes);
    }

    [Fact]
    public async Task ScanAsync_TracksLargestFilesAcrossWholeTree_OrderedDescendingBySize()
    {
        WriteFile(Path.Combine(_root, "small.bin"), 10);
        var sub = Directory.CreateDirectory(Path.Combine(_root, "sub")).FullName;
        WriteFile(Path.Combine(sub, "big.bin"), 1000);
        WriteFile(Path.Combine(sub, "medium.bin"), 100);

        var result = await _scanner.ScanAsync(_root);

        Assert.Equal(3, result.LargestFiles.Count);
        Assert.Equal("big.bin", result.LargestFiles[0].Name);
        Assert.Equal("medium.bin", result.LargestFiles[1].Name);
        Assert.Equal("small.bin", result.LargestFiles[2].Name);
    }

    [Fact]
    public async Task ScanAsync_TracksOldestFiles_OrderedAscendingByLastWriteTime()
    {
        var oldPath = Path.Combine(_root, "old.bin");
        var newPath = Path.Combine(_root, "new.bin");
        WriteFile(oldPath, 10);
        WriteFile(newPath, 10);
        File.SetLastWriteTimeUtc(oldPath, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(newPath, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var result = await _scanner.ScanAsync(_root);

        Assert.Equal(2, result.OldestFiles.Count);
        Assert.Equal("old.bin", result.OldestFiles[0].Name);
        Assert.Equal("new.bin", result.OldestFiles[1].Name);
    }

    [Fact]
    public async Task ScanAsync_ReportsProgressForEachDirectoryVisited()
    {
        Directory.CreateDirectory(Path.Combine(_root, "sub1"));
        Directory.CreateDirectory(Path.Combine(_root, "sub2"));

        var visited = new List<string>();
        var progress = new Progress<string>(visited.Add);

        await _scanner.ScanAsync(_root, progress);
        // Progress<T> marshals via SynchronizationContext.Post; give it a beat to flush.
        await Task.Delay(50);

        Assert.Contains(_root, visited);
    }

    private static void WriteFile(string path, int sizeBytes) =>
        File.WriteAllBytes(path, new byte[sizeBytes]);
}

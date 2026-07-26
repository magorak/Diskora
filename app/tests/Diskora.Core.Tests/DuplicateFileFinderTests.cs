using Diskora.Core.Services;

namespace Diskora.Core.Tests;

public sealed class DuplicateFileFinderTests : IDisposable
{
    private readonly string _root;
    private readonly DuplicateFileFinder _finder = new();

    public DuplicateFileFinderTests()
    {
        _root = Directory.CreateTempSubdirectory("diskora-dup-tests-").FullName;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Best effort cleanup.
        }
    }

    [Fact]
    public async Task FindAsync_IdenticalFilesInDifferentFolders_AreGroupedTogether()
    {
        var sub = Directory.CreateDirectory(Path.Combine(_root, "sub")).FullName;
        WriteFile(Path.Combine(_root, "a.txt"), "obsah");
        WriteFile(Path.Combine(sub, "b.txt"), "obsah");
        WriteFile(Path.Combine(_root, "unique.txt"), "jine");

        var result = await _finder.FindAsync(_root);

        var group = Assert.Single(result);
        Assert.Equal(2, group.FilePaths.Count);
        Assert.Contains(group.FilePaths, p => p.EndsWith("a.txt"));
        Assert.Contains(group.FilePaths, p => p.EndsWith("b.txt"));
    }

    [Fact]
    public async Task FindAsync_SameSizeDifferentContent_AreNotGrouped()
    {
        WriteFile(Path.Combine(_root, "a.txt"), "AAAAA");
        WriteFile(Path.Combine(_root, "b.txt"), "BBBBB");

        var result = await _finder.FindAsync(_root);

        Assert.Empty(result);
    }

    [Fact]
    public async Task FindAsync_ThreeIdenticalFiles_FormOneGroupOfThree()
    {
        WriteFile(Path.Combine(_root, "a.txt"), "trojice");
        WriteFile(Path.Combine(_root, "b.txt"), "trojice");
        WriteFile(Path.Combine(_root, "c.txt"), "trojice");

        var result = await _finder.FindAsync(_root);

        var group = Assert.Single(result);
        Assert.Equal(3, group.FilePaths.Count);
        Assert.Equal(2 * group.SizeBytes, group.ReclaimableBytes);
    }

    [Fact]
    public async Task FindAsync_EmptyFiles_AreIgnored()
    {
        WriteFile(Path.Combine(_root, "empty1.txt"), "");
        WriteFile(Path.Combine(_root, "empty2.txt"), "");

        var result = await _finder.FindAsync(_root);

        Assert.Empty(result);
    }

    [Fact]
    public async Task FindAsync_NoDuplicates_ReturnsEmpty()
    {
        WriteFile(Path.Combine(_root, "a.txt"), "unikat1");
        WriteFile(Path.Combine(_root, "b.txt"), "unikat22");

        var result = await _finder.FindAsync(_root);

        Assert.Empty(result);
    }

    [Fact]
    public async Task FindAsync_ResultsOrderedByReclaimableBytesDescending()
    {
        // Malá skupina duplicit (2x malý soubor) vs. velká skupina (3x větší soubor) -
        // druhá skupina má víc reklamovatelných bajtů, měla by být první.
        WriteFile(Path.Combine(_root, "small1.txt"), "xx");
        WriteFile(Path.Combine(_root, "small2.txt"), "xx");
        WriteFile(Path.Combine(_root, "big1.txt"), "yyyyyyyyyy");
        WriteFile(Path.Combine(_root, "big2.txt"), "yyyyyyyyyy");
        WriteFile(Path.Combine(_root, "big3.txt"), "yyyyyyyyyy");

        var result = await _finder.FindAsync(_root);

        Assert.Equal(2, result.Count);
        Assert.True(result[0].ReclaimableBytes >= result[1].ReclaimableBytes);
        Assert.Equal(3, result[0].FilePaths.Count);
    }

    private static void WriteFile(string path, string content) => File.WriteAllText(path, content);
}

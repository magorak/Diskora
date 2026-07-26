using Diskora.Core.Services;

namespace Diskora.Core.Tests;

public sealed class FragmentationAnalysisServiceTests : IDisposable
{
    private readonly string _root;
    private readonly FragmentationAnalysisService _service = new();

    public FragmentationAnalysisServiceTests()
    {
        _root = Directory.CreateTempSubdirectory("diskora-frag-tests-").FullName;
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
    public async Task AnalyzeAsync_CountsAllNonEmptyFilesAcrossSubdirectories()
    {
        var sub = Directory.CreateDirectory(Path.Combine(_root, "sub")).FullName;
        File.WriteAllText(Path.Combine(_root, "a.txt"), "obsah");
        File.WriteAllText(Path.Combine(sub, "b.txt"), "obsah");

        var result = await _service.AnalyzeAsync(_root);

        Assert.Equal(2, result.FilesScanned);
    }

    [Fact]
    public async Task AnalyzeAsync_EmptyFiles_AreExcludedFromScan()
    {
        File.WriteAllText(Path.Combine(_root, "empty.txt"), "");
        File.WriteAllText(Path.Combine(_root, "nonempty.txt"), "obsah");

        var result = await _service.AnalyzeAsync(_root);

        Assert.Equal(1, result.FilesScanned);
    }

    [Fact]
    public async Task AnalyzeAsync_NoFiles_ReturnsEmptyResult()
    {
        var result = await _service.AnalyzeAsync(_root);

        Assert.Equal(0, result.FilesScanned);
        Assert.Equal(0, result.FragmentedFileCount);
        Assert.Empty(result.MostFragmentedFiles);
    }

    [Fact]
    public async Task AnalyzeAsync_FreshlyWrittenSmallFiles_AreNotReportedAsFragmented()
    {
        // Čerstvě zapsané malé soubory na běžně nezaplněném svazku by měly mít
        // jeden souvislý rozsah clusterů - ověřuje, že se skutečné IOCTL volání
        // nechová nesmyslně (nehlásí fragmentaci tam, kde žádná není).
        for (var i = 0; i < 5; i++)
        {
            File.WriteAllText(Path.Combine(_root, $"file{i}.txt"), new string('x', 1024));
        }

        var result = await _service.AnalyzeAsync(_root);

        Assert.Equal(5, result.FilesScanned);
        Assert.Equal(0, result.FragmentedFileCount);
        Assert.Empty(result.MostFragmentedFiles);
    }

    [Fact]
    public async Task AnalyzeAsync_NonexistentRoot_ReturnsEmptyResultWithoutThrowing()
    {
        var result = await _service.AnalyzeAsync(Path.Combine(_root, "does-not-exist"));

        Assert.Equal(0, result.FilesScanned);
    }
}

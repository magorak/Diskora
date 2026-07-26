namespace Diskora.Core.Models;

public sealed record FragmentationAnalysisResult(
    int FilesScanned,
    int FragmentedFileCount,
    IReadOnlyList<FragmentedFileEntry> MostFragmentedFiles);

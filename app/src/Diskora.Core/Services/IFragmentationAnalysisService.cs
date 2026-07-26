using Diskora.Core.Models;

namespace Diskora.Core.Services;

public interface IFragmentationAnalysisService
{
    Task<FragmentationAnalysisResult> AnalyzeAsync(
        string rootPath,
        IProgress<string>? onFileScanned = null,
        CancellationToken cancellationToken = default);
}

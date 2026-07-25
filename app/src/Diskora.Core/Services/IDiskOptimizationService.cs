using Diskora.Core.Models;

namespace Diskora.Core.Services;

public interface IDiskOptimizationService
{
    DiskOptimizationCapabilities GetCapabilities(string driveLetter);

    Task<OptimizationRunOutcome> RunTrimAsync(
        string driveLetter, IProgress<string>? onOutputLine = null, CancellationToken cancellationToken = default);

    Task<OptimizationRunOutcome> RunDefragmentAsync(
        string driveLetter, IProgress<string>? onOutputLine = null, CancellationToken cancellationToken = default);
}

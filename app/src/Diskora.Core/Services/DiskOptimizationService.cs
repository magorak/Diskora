using Diskora.Core.Models;
using Diskora.Native.Storage;
using Diskora.Repair;

namespace Diskora.Core.Services;

public sealed class DiskOptimizationService : IDiskOptimizationService
{
    public DiskOptimizationCapabilities GetCapabilities(string driveLetter) => new(
        StoragePropertyReader.HasSeekPenalty(driveLetter),
        StoragePropertyReader.SupportsTrim(driveLetter));

    public async Task<OptimizationRunOutcome> RunTrimAsync(
        string driveLetter, IProgress<string>? onOutputLine = null, CancellationToken cancellationToken = default)
    {
        var result = await DefragRunner.RunTrimAsync(driveLetter, onOutputLine, cancellationToken);
        return Map(result);
    }

    public async Task<OptimizationRunOutcome> RunDefragmentAsync(
        string driveLetter, IProgress<string>? onOutputLine = null, CancellationToken cancellationToken = default)
    {
        var result = await DefragRunner.RunDefragmentAsync(driveLetter, onOutputLine, cancellationToken);
        return Map(result);
    }

    private static OptimizationRunOutcome Map(DefragRunResult result) =>
        new(result.Started, result.FailureReason, result.ExitCode, result.OutputLines);
}

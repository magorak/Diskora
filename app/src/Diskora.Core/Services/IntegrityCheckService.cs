using Diskora.Core.Models;
using Diskora.Native.Fsctl;
using Diskora.Repair;

namespace Diskora.Core.Services;

public sealed class IntegrityCheckService : IIntegrityCheckService
{
    public VolumeDirtyState CheckDirtyState(string driveLetter)
    {
        var isDirty = VolumeDirtyChecker.IsDirty(driveLetter);
        return isDirty switch
        {
            true => VolumeDirtyState.Dirty,
            false => VolumeDirtyState.Clean,
            null => VolumeDirtyState.Unknown,
        };
    }

    public async Task<IntegrityScanOutcome> RunReadOnlyScanAsync(
        string driveLetter,
        IProgress<string>? onOutputLine = null,
        CancellationToken cancellationToken = default)
    {
        var result = await ChkdskRunner.RunReadOnlyScanAsync(driveLetter, onOutputLine, cancellationToken);
        return new IntegrityScanOutcome(result.Started, result.FailureReason, result.ExitCode, result.OutputLines);
    }
}

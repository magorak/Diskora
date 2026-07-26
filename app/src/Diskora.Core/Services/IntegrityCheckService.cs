using Diskora.Core.Models;
using Diskora.Native.Fsctl;
using Diskora.Repair;

namespace Diskora.Core.Services;

public sealed class IntegrityCheckService(IDiskHistoryStore? historyStore = null) : IIntegrityCheckService
{
    public VolumeDirtyState CheckDirtyState(string driveLetter)
    {
        var state = ReadDirtyState(driveLetter);
        historyStore?.RecordIntegrityCheck(driveLetter, state, scanExitCode: null, scanAppearsClean: null);
        return state;
    }

    public async Task<IntegrityScanOutcome> RunReadOnlyScanAsync(
        string driveLetter,
        IProgress<string>? onOutputLine = null,
        CancellationToken cancellationToken = default)
    {
        var result = await ChkdskRunner.RunReadOnlyScanAsync(driveLetter, onOutputLine, cancellationToken);
        var outcome = new IntegrityScanOutcome(result.Started, result.FailureReason, result.ExitCode, result.OutputLines);

        var state = ReadDirtyState(driveLetter);
        historyStore?.RecordIntegrityCheck(driveLetter, state, outcome.ExitCode, outcome.Started ? outcome.AppearsClean : null);

        return outcome;
    }

    public async Task<IntegrityScanOutcome> RunSpotFixAsync(
        string driveLetter,
        IProgress<string>? onOutputLine = null,
        CancellationToken cancellationToken = default)
    {
        var result = await ChkdskRunner.RunSpotFixAsync(driveLetter, onOutputLine, cancellationToken);
        var outcome = new IntegrityScanOutcome(result.Started, result.FailureReason, result.ExitCode, result.OutputLines);

        var state = ReadDirtyState(driveLetter);
        historyStore?.RecordIntegrityCheck(driveLetter, state, outcome.ExitCode, outcome.Started ? outcome.AppearsClean : null);

        return outcome;
    }

    private static VolumeDirtyState ReadDirtyState(string driveLetter) => VolumeDirtyChecker.IsDirty(driveLetter) switch
    {
        true => VolumeDirtyState.Dirty,
        false => VolumeDirtyState.Clean,
        null => VolumeDirtyState.Unknown,
    };
}

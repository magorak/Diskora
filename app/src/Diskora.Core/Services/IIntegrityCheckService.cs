using Diskora.Core.Models;

namespace Diskora.Core.Services;

public interface IIntegrityCheckService
{
    VolumeDirtyState CheckDirtyState(string driveLetter);

    Task<IntegrityScanOutcome> RunReadOnlyScanAsync(
        string driveLetter,
        IProgress<string>? onOutputLine = null,
        CancellationToken cancellationToken = default);
}

using Diskora.Core.Models;

namespace Diskora.Core.Services;

public interface IIntegrityCheckService
{
    VolumeDirtyState CheckDirtyState(string driveLetter);

    Task<IntegrityScanOutcome> RunReadOnlyScanAsync(
        string driveLetter,
        IProgress<string>? onOutputLine = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Skutečná oprava (`Repair-Volume -SpotFix`) - na rozdíl od
    /// <see cref="RunReadOnlyScanAsync"/> SKUTEČNĚ ZAPISUJE na disk. Volající
    /// (UI) musí mít vlastní explicitní potvrzení PŘED zavoláním.
    /// </summary>
    Task<IntegrityScanOutcome> RunSpotFixAsync(
        string driveLetter,
        IProgress<string>? onOutputLine = null,
        CancellationToken cancellationToken = default);
}

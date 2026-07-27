using Diskora.Core.Diagnostics;
using Diskora.Core.Models;

namespace Diskora.Core.Services;

/// <summary>
/// Posbírá data ze služeb, které už existují a jsou ověřené (S.M.A.R.T.,
/// dirty bit, zjištění typu disku), a nechá <see cref="DiskDoctorAdvisor"/>
/// udělat závěr. Sama žádnou vlastní byznys logiku nemá - to je celý smysl:
/// Disk Doctor nepřidává novou cestu k datům, jen spojuje existující do
/// jednoho verdiktu.
/// </summary>
public sealed class DiskDoctorService(
    ISmartService smartService,
    IIntegrityCheckService integrityService,
    IDiskOptimizationService optimizationService,
    Func<bool> isRunningAsAdministrator) : IDiskDoctorService
{
    public async Task<DiskDoctorReport> RunAsync(
        string driveLetter,
        int? physicalDiskIndex,
        string subject,
        CancellationToken cancellationToken = default)
    {
        // Všechno jsou to blokující IOCTL/WMI volání - patří mimo UI vlákno.
        return await Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Bez známého fyzického disku se S.M.A.R.T. přeskočí - poradce to
                // pozná podle IsSupported=false a napíše, že tahle část chybí.
                var smart = physicalDiskIndex is { } index
                    ? smartService.ReadReport(index)
                    : new SmartReadResult(false, "Svazek se nepodařilo přiřadit k fyzickému disku.", null);

                cancellationToken.ThrowIfCancellationRequested();
                var dirtyState = integrityService.CheckDirtyState(driveLetter);

                cancellationToken.ThrowIfCancellationRequested();
                var capabilities = optimizationService.GetCapabilities(driveLetter);

                var inputs = new DiskDoctorInputs(smart, dirtyState, capabilities, isRunningAsAdministrator());
                return DiskDoctorAdvisor.Diagnose(subject, inputs);
            },
            cancellationToken).ConfigureAwait(false);
    }
}

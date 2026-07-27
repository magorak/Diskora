using Diskora.Core.Models;

namespace Diskora.Core.Services;

public interface IDiskDoctorService
{
    /// <summary>
    /// Projde svazek a fyzický disk pod ním a vrátí souhrn. Čistě diagnostické -
    /// nic nespouští a na disk nezapisuje, takže se dá pustit kdykoli bez obav.
    /// </summary>
    Task<DiskDoctorReport> RunAsync(
        string driveLetter,
        int? physicalDiskIndex,
        string subject,
        CancellationToken cancellationToken = default);
}

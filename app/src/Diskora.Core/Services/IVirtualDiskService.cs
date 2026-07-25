using Diskora.Core.Models;

namespace Diskora.Core.Services;

public interface IVirtualDiskService
{
    VirtualDiskReadOutcome ReadInfo(string path);

    VirtualDiskOperationOutcome Attach(string path, bool readOnly);

    VirtualDiskOperationOutcome Detach(string path);

    /// <summary>Připojí ISO jako virtuální CD/DVD - na rozdíl od VHD/VHDX nevyžaduje admin práva.</summary>
    Task<IsoMountOutcome> MountIsoAsync(string isoPath, CancellationToken cancellationToken = default);

    Task<VirtualDiskOperationOutcome> DismountIsoAsync(string isoPath, CancellationToken cancellationToken = default);
}

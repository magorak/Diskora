using Diskora.Core.Models;
using Diskora.Repair;
using Diskora.VirtualDisks;

namespace Diskora.Core.Services;

public sealed class VirtualDiskService : IVirtualDiskService
{
    public VirtualDiskReadOutcome ReadInfo(string path)
    {
        var result = VirtualDiskReader.GetInfo(path);
        if (!result.Success || result.Info is null)
        {
            return new VirtualDiskReadOutcome(false, result.FailureReason, null);
        }

        var summary = new VirtualDiskSummary(
            result.Info.Path,
            MapFormat(result.Info.Format),
            result.Info.VirtualSizeBytes,
            result.Info.PhysicalSizeBytes,
            result.Info.BlockSizeBytes,
            result.Info.SectorSizeBytes);

        return new VirtualDiskReadOutcome(true, null, summary);
    }

    public VirtualDiskOperationOutcome Attach(string path, bool readOnly)
    {
        var result = VirtualDiskAttacher.Attach(path, readOnly);
        return new VirtualDiskOperationOutcome(result.Success, result.FailureReason);
    }

    public VirtualDiskOperationOutcome Detach(string path)
    {
        var result = VirtualDiskAttacher.Detach(path);
        return new VirtualDiskOperationOutcome(result.Success, result.FailureReason);
    }

    public async Task<IsoMountOutcome> MountIsoAsync(string isoPath, CancellationToken cancellationToken = default)
    {
        var result = await IsoMounter.MountAsync(isoPath, cancellationToken);
        return new IsoMountOutcome(result.Success, result.FailureReason, result.DriveLetter);
    }

    public async Task<VirtualDiskOperationOutcome> DismountIsoAsync(string isoPath, CancellationToken cancellationToken = default)
    {
        var result = await IsoMounter.DismountAsync(isoPath, cancellationToken);
        return new VirtualDiskOperationOutcome(result.Success, result.FailureReason);
    }

    private static Models.VirtualDiskFormat MapFormat(VirtualDisks.VirtualDiskFormat format) => format switch
    {
        VirtualDisks.VirtualDiskFormat.Vhd => Models.VirtualDiskFormat.Vhd,
        VirtualDisks.VirtualDiskFormat.Vhdx => Models.VirtualDiskFormat.Vhdx,
        VirtualDisks.VirtualDiskFormat.Iso => Models.VirtualDiskFormat.Iso,
        _ => Models.VirtualDiskFormat.Unknown,
    };
}

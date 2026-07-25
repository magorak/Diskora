using Diskora.Core.Models;

namespace Diskora.Core.Services;

public interface IDiskEnumerationService
{
    IReadOnlyList<PhysicalDiskInfo> GetPhysicalDisks();

    IReadOnlyList<VolumeInfo> GetVolumes();
}

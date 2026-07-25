using Diskora.Core.Models;

namespace Diskora.Core.Services;

public interface IDiskEventLogService
{
    IReadOnlyList<DiskEventLogEntry> GetRecentDiskEvents(int maxEntries = 50);
}

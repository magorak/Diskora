using Diskora.Core.Models;
using Diskora.Native.EventLog;

namespace Diskora.Core.Services;

public sealed class DiskEventLogService : IDiskEventLogService
{
    public IReadOnlyList<DiskEventLogEntry> GetRecentDiskEvents(int maxEntries = 50) =>
        DiskEventLogReader.GetRecentDiskEvents(maxEntries)
            .Select(native => new DiskEventLogEntry(
                native.TimeCreated,
                DiskEventLevelMapper.FromRawLevel(native.RawLevel),
                native.LogName,
                native.ProviderName,
                native.EventId,
                native.Message))
            .ToList();
}

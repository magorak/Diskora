namespace Diskora.Core.Models;

public sealed record SmartHistoryEntry(long Id, int DiskIndex, DateTimeOffset RecordedAtUtc, DiskHealthStatus OverallHealth);

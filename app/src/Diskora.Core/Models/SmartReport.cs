namespace Diskora.Core.Models;

public sealed record SmartReport(
    int DiskIndex,
    DateTimeOffset ReadAtUtc,
    IReadOnlyList<SmartAttributeReading> Attributes,
    DiskHealthStatus OverallHealth);

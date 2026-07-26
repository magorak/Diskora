namespace Diskora.Core.Models;

/// <summary>
/// Zdravotní report jednoho fyzického disku. Podle typu disku je naplněná právě
/// jedna z cest: <paramref name="Attributes"/> u ATA/SATA disků,
/// <paramref name="NvmeHealth"/> u NVMe disků. <paramref name="OverallHealth"/>
/// je společný verdikt bez ohledu na to, odkud data přišla.
/// </summary>
public sealed record SmartReport(
    int DiskIndex,
    DateTimeOffset ReadAtUtc,
    IReadOnlyList<SmartAttributeReading> Attributes,
    DiskHealthStatus OverallHealth,
    NvmeHealthInfo? NvmeHealth = null);

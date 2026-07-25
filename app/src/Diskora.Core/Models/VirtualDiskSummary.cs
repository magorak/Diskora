namespace Diskora.Core.Models;

public sealed record VirtualDiskSummary(
    string Path,
    VirtualDiskFormat Format,
    ulong VirtualSizeBytes,
    ulong PhysicalSizeBytes,
    uint BlockSizeBytes,
    uint SectorSizeBytes);

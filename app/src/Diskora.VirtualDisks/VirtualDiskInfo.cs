namespace Diskora.VirtualDisks;

public sealed record VirtualDiskInfo(
    string Path,
    VirtualDiskFormat Format,
    ulong VirtualSizeBytes,
    ulong PhysicalSizeBytes,
    uint BlockSizeBytes,
    uint SectorSizeBytes);

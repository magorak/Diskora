namespace Diskora.Core.Models;

public sealed record VolumeInfo(
    string Name,
    string? Label,
    string? FileSystem,
    long TotalSizeBytes,
    long FreeSpaceBytes,
    DriveType DriveType,
    int? PhysicalDiskIndex);

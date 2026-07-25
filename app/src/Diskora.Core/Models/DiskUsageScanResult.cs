namespace Diskora.Core.Models;

public sealed record DiskUsageScanResult(
    DirectoryUsageNode Root,
    IReadOnlyList<FileUsageEntry> LargestFiles,
    IReadOnlyList<FileUsageEntry> OldestFiles);

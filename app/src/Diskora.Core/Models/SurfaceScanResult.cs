namespace Diskora.Core.Models;

public sealed record SurfaceScanResult(
    bool Started,
    string? FailureReason,
    long BytesScanned,
    long TotalBytes,
    IReadOnlyList<BadSectorRange> BadRanges)
{
    public bool AppearsClean => Started && BadRanges.Count == 0;
}

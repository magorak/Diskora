namespace Diskora.Native.Storage;

public readonly record struct NativeBadRange(long OffsetBytes, long LengthBytes);

public sealed record NativeSurfaceScanResult(
    bool Success,
    string? FailureReason,
    long BytesScanned,
    IReadOnlyList<NativeBadRange> BadRanges);

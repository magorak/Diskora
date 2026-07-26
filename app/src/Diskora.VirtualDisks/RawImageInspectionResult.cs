namespace Diskora.VirtualDisks;

public sealed record RawImageInspectionResult(bool Success, string? FailureReason, RawImagePartitionScheme Scheme, int PartitionCount);

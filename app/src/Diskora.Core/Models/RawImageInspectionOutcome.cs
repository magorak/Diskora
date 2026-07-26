namespace Diskora.Core.Models;

public enum RawImagePartitionScheme
{
    Unknown,
    Mbr,
    Gpt,
}

public sealed record RawImageInspectionOutcome(bool Success, string? FailureReason, RawImagePartitionScheme Scheme, int PartitionCount);

namespace Diskora.VirtualDisks;

public sealed record VirtualDiskReadResult(bool Success, string? FailureReason, VirtualDiskInfo? Info);

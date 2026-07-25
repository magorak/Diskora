namespace Diskora.Core.Models;

public sealed record VirtualDiskOperationOutcome(bool Success, string? FailureReason);

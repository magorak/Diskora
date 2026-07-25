namespace Diskora.Core.Models;

public sealed record VirtualDiskReadOutcome(bool Success, string? FailureReason, VirtualDiskSummary? Summary);

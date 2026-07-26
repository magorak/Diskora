namespace Diskora.Repair;

public sealed record ScheduledTaskResult(bool Started, string? FailureReason, int? ExitCode, IReadOnlyList<string> OutputLines);

namespace Diskora.Repair;

public sealed record DefragRunResult(bool Started, string? FailureReason, int? ExitCode, IReadOnlyList<string> OutputLines);

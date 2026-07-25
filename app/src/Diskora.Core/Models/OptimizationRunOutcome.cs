namespace Diskora.Core.Models;

public sealed record OptimizationRunOutcome(bool Started, string? FailureReason, int? ExitCode, IReadOnlyList<string> OutputLines);

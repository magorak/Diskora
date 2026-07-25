namespace Diskora.Repair;

public sealed record ChkdskScanResult(bool Started, string? FailureReason, int? ExitCode, IReadOnlyList<string> OutputLines);

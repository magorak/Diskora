namespace Diskora.Core.Models;

public sealed record IntegrityScanOutcome(
    bool Started,
    string? FailureReason,
    int? ExitCode,
    IReadOnlyList<string> OutputLines)
{
    public bool AppearsClean => Started && ExitCode == 0;
}

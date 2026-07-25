namespace Diskora.Repair;

/// <summary>
/// Spouští `chkdsk /scan` - needestruktivní online kontrolu (Windows 8+),
/// která nevyžaduje odpojení svazku a nic neopravuje, jen hlásí nález.
/// Skutečná oprava (/f, /spotfix) je záměrně samostatný, dosud nepropojený
/// krok - vyžaduje vlastní explicitní potvrzení v UI, protože může vést
/// k naplánovanému restartu.
/// </summary>
public static class ChkdskRunner
{
    public static async Task<ChkdskScanResult> RunReadOnlyScanAsync(
        string driveLetter,
        IProgress<string>? onOutputLine = null,
        CancellationToken cancellationToken = default)
    {
        var target = driveLetter.TrimEnd('\\', ':') + ":";
        var outcome = await ProcessOutputRunner.RunAsync("chkdsk.exe", [target, "/scan"], onOutputLine, cancellationToken);
        return new ChkdskScanResult(outcome.Started, outcome.FailureReason, outcome.ExitCode, outcome.OutputLines);
    }
}

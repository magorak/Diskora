namespace Diskora.Repair;

/// <summary>
/// Připojuje/odpojuje ISO obrazy jako virtuální CD/DVD přes orchestraci
/// `Mount-DiskImage`/`Dismount-DiskImage`. Cesta k souboru jde vždy přes
/// proměnnou prostředí, ne interpolaci do PowerShell příkazu - viz
/// docs/SECURITY.md, prevence injection.
///
/// Na rozdíl od VHD/VHDX (Diskora.VirtualDisks, přímé `virtdisk.dll`)
/// nejde o P/Invoke: živě ověřeno, že `AttachVirtualDisk` s
/// VIRTUAL_STORAGE_TYPE_DEVICE_ISO sice vrátí úspěch (a bez admin práv!),
/// ale výsledná jednotka zůstane bez souborového systému - chybí krok,
/// který `Mount-DiskImage` dělá navíc a není přes zdokumentované
/// virtdisk.dll/WMI API dostupný samostatně. Mount-DiskImage navíc na
/// rozdíl od VHD/VHDX připojení nevyžaduje admin práva - ověřeno živě.
/// </summary>
public static class IsoMounter
{
    private const string PathEnvironmentVariable = "DISKORA_ISO_PATH";

    public static async Task<IsoMountResult> MountAsync(string isoPath, CancellationToken cancellationToken = default)
    {
        const string script =
            "$path = $env:DISKORA_ISO_PATH; " +
            "$image = Mount-DiskImage -ImagePath $path -PassThru; " +
            "$volume = $image | Get-Volume; " +
            "if ($volume) { Write-Output $volume.DriveLetter }";

        var outcome = await RunAsync(script, isoPath, cancellationToken);

        if (!outcome.Started || outcome.ExitCode != 0)
        {
            return new IsoMountResult(false, DescribeFailure(outcome), null);
        }

        var driveLetter = outcome.OutputLines.LastOrDefault(l => !string.IsNullOrWhiteSpace(l))?.Trim();
        return string.IsNullOrEmpty(driveLetter)
            ? new IsoMountResult(false, "Obraz se připojil, ale nepodařilo se zjistit písmeno jednotky.", null)
            : new IsoMountResult(true, null, driveLetter);
    }

    public static async Task<IsoMountResult> DismountAsync(string isoPath, CancellationToken cancellationToken = default)
    {
        const string script = "$path = $env:DISKORA_ISO_PATH; Dismount-DiskImage -ImagePath $path";

        var outcome = await RunAsync(script, isoPath, cancellationToken);

        return !outcome.Started || outcome.ExitCode != 0
            ? new IsoMountResult(false, DescribeFailure(outcome), null)
            : new IsoMountResult(true, null, null);
    }

    private static Task<ProcessRunOutcome> RunAsync(string script, string isoPath, CancellationToken cancellationToken) =>
        ProcessOutputRunner.RunAsync(
            "powershell.exe",
            ["-NoProfile", "-NonInteractive", "-Command", script],
            onOutputLine: null,
            cancellationToken,
            environmentVariables: new Dictionary<string, string> { [PathEnvironmentVariable] = isoPath });

    private static string DescribeFailure(ProcessRunOutcome outcome) => outcome.Started
        ? $"Operace selhala (návratový kód {outcome.ExitCode}). {string.Join(' ', outcome.OutputLines).Trim()}"
        : outcome.FailureReason ?? "Neznámá chyba.";
}

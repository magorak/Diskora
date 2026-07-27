namespace Diskora.Repair;

/// <summary>
/// Spouští `chkdsk /scan` - needestruktivní online kontrolu (Windows 8+),
/// která nevyžaduje odpojení svazku a nic neopravuje, jen hlásí nález.
/// Skutečná oprava je záměrně samostatný krok s vlastním potvrzením v UI
/// (viz <see cref="RunSpotFixAsync"/>) - jen jeho "spotfix" varianta, protože
/// na rozdíl od `/f`/`/r` (které svazek potřebují uzamknout a na systémovém
/// svazku by mohly vyžadovat naplánovaný restart) `Repair-Volume -SpotFix`
/// je online oprava (Windows 8+ self-healing NTFS) bez potřeby odpojení ve
/// většině případů - nevyžaduje tak zatím řešit složitější UX kolem
/// plánovaného restartu.
/// </summary>
public static class ChkdskRunner
{
    private const string DriveLetterEnvironmentVariable = "DISKORA_DRIVE_LETTER";

    public static async Task<ChkdskScanResult> RunReadOnlyScanAsync(
        string driveLetter,
        IProgress<string>? onOutputLine = null,
        CancellationToken cancellationToken = default)
    {
        var target = driveLetter.TrimEnd('\\', ':') + ":";
        var outcome = await ProcessOutputRunner.RunAsync("chkdsk.exe", [target, "/scan"], onOutputLine, cancellationToken);
        return new ChkdskScanResult(outcome.Started, outcome.FailureReason, outcome.ExitCode, outcome.OutputLines);
    }

    /// <summary>
    /// `Repair-Volume -SpotFix` - opraví běžné poškození (osiřelé soubory,
    /// poškozené indexy/bezpečnostní deskriptory) bez potřeby odpojit svazek
    /// ve většině případů. Na rozdíl od `RunReadOnlyScanAsync` SKUTEČNĚ ZAPISUJE
    /// na disk - volající (UI) musí mít vlastní explicitní potvrzení PŘED
    /// zavoláním. Cesta jde přes proměnnou prostředí, ne interpolaci do
    /// PowerShell příkazu - viz docs/SECURITY.md, prevence injection (stejný
    /// vzor jako IsoMounter).
    /// </summary>
    public static async Task<ChkdskScanResult> RunSpotFixAsync(
        string driveLetter,
        IProgress<string>? onOutputLine = null,
        CancellationToken cancellationToken = default)
    {
        // $ErrorActionPreference = 'Stop' uvnitř try bloku je nutné navíc k -ErrorAction Stop
        // na samotném volání - živě zjištěno, že bez práv administrátora Repair-Volume
        // někdy selže jen jako NEterminating chyba (do error streamu, ne výjimka) a $result
        // zůstane null; bez tohohle by se to tiše prohlásilo za úspěch. Explicitní kontrola
        // $result níže je obrana do hloubky pro stejný scénář.
        //
        // POZOR na tvar výsledku: Repair-Volume vrací PŘÍMO hodnotu typu RepairStatus
        // (např. "NoErrorsFound"), ne objekt s vlastností HealthStatus. Původní verze
        // kontrolovala $result.HealthStatus, což je vždycky prázdné - a protože je to
        // uvnitř try bloku, každá úspěšná oprava skončila vyhozenou výjimkou a exit 1.
        // Odhaleno až prvním živým spuštěním s právy administrátora.
        const string script = """
            try {
                $ErrorActionPreference = 'Stop'
                $result = Repair-Volume -DriveLetter $env:DISKORA_DRIVE_LETTER -SpotFix -ErrorAction Stop
                if ($null -eq $result -or [string]::IsNullOrWhiteSpace([string]$result)) {
                    throw "Repair-Volume nevrátil žádný výsledek (chybí oprávnění administrátora?)."
                }
                Write-Output "Stav opravy: $result"
            } catch {
                Write-Error $_.Exception.Message
                exit 1
            }
            """;

        var outcome = await ProcessOutputRunner.RunAsync(
            "powershell.exe",
            ["-NoProfile", "-NonInteractive", "-Command", script],
            onOutputLine,
            cancellationToken,
            environmentVariables: new Dictionary<string, string> { [DriveLetterEnvironmentVariable] = driveLetter.TrimEnd('\\', ':') });

        return new ChkdskScanResult(outcome.Started, outcome.FailureReason, outcome.ExitCode, outcome.OutputLines);
    }
}

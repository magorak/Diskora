using System.Runtime.InteropServices;
using System.Text;

namespace Diskora.Repair;

/// <summary>
/// Registruje/ruší pravidelnou kontrolu zdraví disků ve Windows Plánovači úloh
/// přes `schtasks.exe` - spustí `diskora.exe healthcheck` jednou denně v zadaný
/// čas, i když GUI zrovna neběží (na rozdíl od `Diskora.App.Tray.DiskHealthNotifier`,
/// který kontroluje jen po dobu běhu GUI). Bez `/RU`/`/RP` (spustit jako) se úloha
/// vytvoří pod aktuálním uživatelem - nevyžaduje admin práva. Cesta k `diskora.exe`
/// jde vždy jako samostatný argument (`ArgumentList`, ne skládání příkazového
/// řádku) - viz docs/SECURITY.md; jediné místo, kde se ručně skládá dílčí řetězec,
/// je hodnota /TR (schtasks vlastní mini příkazová řádka), a to jen z DŮVĚRYHODNÉ
/// cesty vlastního spustitelného souboru, ne z uživatelského vstupu.
/// </summary>
public static class ScheduledTaskManager
{
    public const string TaskName = "DiskoraHealthCheck";

    // Stejný problém jako u defrag.exe (viz DefragRunner) - schtasks.exe píše do
    // přesměrovaného streamu v OEM kódové stránce konzole, bez explicitního
    // nastavení se diakritika rozbíjí na mojibake. Živě ověřeno.
    private static readonly Encoding OutputEncoding = ResolveOemEncoding();

    public static Task<ScheduledTaskResult> InstallAsync(
        string executablePath, string time, CancellationToken cancellationToken = default) =>
        RunAsync(
            ["/Create", "/TN", TaskName, "/TR", $"\"{executablePath}\" healthcheck", "/SC", "DAILY", "/ST", time, "/F"],
            cancellationToken);

    public static Task<ScheduledTaskResult> RemoveAsync(CancellationToken cancellationToken = default) =>
        RunAsync(["/Delete", "/TN", TaskName, "/F"], cancellationToken);

    public static Task<ScheduledTaskResult> QueryAsync(CancellationToken cancellationToken = default) =>
        RunAsync(["/Query", "/TN", TaskName, "/FO", "LIST"], cancellationToken);

    private static async Task<ScheduledTaskResult> RunAsync(IEnumerable<string> arguments, CancellationToken cancellationToken)
    {
        var outcome = await ProcessOutputRunner.RunAsync(
            "schtasks.exe", arguments, onOutputLine: null, cancellationToken, OutputEncoding);
        return new ScheduledTaskResult(outcome.Started, outcome.FailureReason, outcome.ExitCode, outcome.OutputLines);
    }

    private static Encoding ResolveOemEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding((int)GetOEMCP());
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetOEMCP();
}

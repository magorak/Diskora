using System.Runtime.InteropServices;
using System.Text;

namespace Diskora.Repair;

/// <summary>
/// Orchestruje `defrag.exe` pro TRIM (SSD) i tradiční defragmentaci (HDD).
/// `/L` provede retrim na discích podporujících TRIM (totéž co Windows
/// "Optimalizovat jednotky" dělá pravidelně na pozadí u SSD). `/D` provede
/// tradiční defragmentaci - má smysl jen u rotačních HDD, u SSD by jen
/// zbytečně opotřebovávala buňky, proto o volbě rozhoduje volající podle
/// zjištěného typu disku (StoragePropertyReader v Diskora.Native).
///
/// Na rozdíl od chkdsk.exe (které při přesměrování výstupu píše rovnou
/// UTF-8/ASCII bezpečný text) defrag.exe zapisuje do přesměrovaného
/// streamu v OEM kódové stránce konzole (u české lokalizace typicky
/// CP852) - bez explicitního nastavení kódování se diakritika rozbíjela
/// na mojibake. Ověřeno živě proti reálnému výstupu.
/// </summary>
public static class DefragRunner
{
    private static readonly Encoding OutputEncoding = ProcessOutputRunner.ConsoleOutputEncoding;

    public static Task<DefragRunResult> RunTrimAsync(
        string driveLetter, IProgress<string>? onOutputLine = null, CancellationToken cancellationToken = default) =>
        RunAsync(driveLetter, "/L", onOutputLine, cancellationToken);

    public static Task<DefragRunResult> RunDefragmentAsync(
        string driveLetter, IProgress<string>? onOutputLine = null, CancellationToken cancellationToken = default) =>
        RunAsync(driveLetter, "/D", onOutputLine, cancellationToken);

    private static async Task<DefragRunResult> RunAsync(
        string driveLetter, string mode, IProgress<string>? onOutputLine, CancellationToken cancellationToken)
    {
        var target = driveLetter.TrimEnd('\\', ':') + ":";
        var outcome = await ProcessOutputRunner.RunAsync(
            "defrag.exe", [target, mode, "/V"], onOutputLine, cancellationToken, OutputEncoding);
        return new DefragRunResult(outcome.Started, outcome.FailureReason, outcome.ExitCode, outcome.OutputLines);
    }

}

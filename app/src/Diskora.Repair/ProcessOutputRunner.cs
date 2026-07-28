using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Diskora.Repair;

internal sealed record ProcessRunOutcome(bool Started, string? FailureReason, int? ExitCode, List<string> OutputLines);

/// <summary>
/// Sdílená logika spouštění konzolového procesu se streamovaným výstupem,
/// použitá <see cref="ChkdskRunner"/> i <see cref="DefragRunner"/>. Argumenty
/// jdou vždy přes ArgumentList (ne skládání stringu) - viz docs/SECURITY.md.
/// </summary>
internal static class ProcessOutputRunner
{
    /// <summary>ANSI stranka (GetACP, cesky 1250). Takhle pise presmerovany vystup chkdsk.exe.</summary>
    public static Encoding AnsiEncoding { get; } = Resolve(GetACP());

    /// <summary>OEM stranka (GetOEMCP, cesky 852). Takhle pise defrag.exe a schtasks.exe.</summary>
    public static Encoding OemEncoding { get; } = Resolve(GetOEMCP());

    /// <summary>
    /// Kodovani je VLASTNOST KONKRETNIHO NASTROJE, ne systemu - proto dve
    /// vlastnosti vyse a zadna spolecna. Overeno na syrovych bajtech
    /// presmerovaneho vystupu: defrag posila pro pismeno y s carkou bajt 0xEC
    /// (CP852), zatimco chkdsk tutez diakritiku pise v CP1250. Jedno spolecne
    /// nastaveni proto vzdycky rozbilo ten druhy nastroj - vyslo najevo az pote,
    /// co si uzivatel vsiml rozsypane cestiny ve vystupu chkdsk.
    ///
    /// Urcovat kodovani podle toho, jak text VYPADA v konzoli, nefunguje - ta si
    /// ho sama prekoduje a svede na spatnou stopu. Rozhoduji bajty.
    /// Konzolova stranka (GetConsoleOutputCP) se neuplatni vubec, protoze
    /// Diskora cte vystup pres rouru, ne z realne konzole.
    /// </summary>
    private static Encoding Resolve(uint codePage)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        try
        {
            return codePage == 0 ? Encoding.UTF8 : Encoding.GetEncoding((int)codePage);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return Encoding.UTF8;
        }
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetOEMCP();

    [DllImport("kernel32.dll")]
    private static extern uint GetACP();

    public static async Task<ProcessRunOutcome> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        IProgress<string>? onOutputLine,
        CancellationToken cancellationToken,
        Encoding? outputEncoding = null,
        IReadOnlyDictionary<string, string>? environmentVariables = null)
    {
        var outputLines = new List<string>();

        // OutputDataReceived a ErrorDataReceived se vyvolávají KAŽDÝ ZE SVÉHO vlákna
        // (jedno čtecí vlákno na stream), takže se do seznamu přidává souběžně.
        // List<T>.Add souběh nesnáší - dvě vlákna mohou zapsat na stejný index nebo
        // trefit zvětšování pole a část řádků se ztratí či zdvojí. Projeví se to jen
        // u procesů, které skutečně píšou do obou streamů (PowerShell orchestrace při
        // chybě, chkdsk), takže je to tichá a nepravidelná chyba.
        var outputLock = new Lock();

        void OnLine(string? line)
        {
            if (line is null)
            {
                return;
            }

            lock (outputLock)
            {
                outputLines.Add(line);
            }

            // Hlášení se schválně dělá MIMO zámek - IProgress.Report u GUI
            // marshalluje na UI vlákno a držet přitom zámek by zbytečně blokovalo
            // druhý stream.
            onOutputLine?.Report(line);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        if (outputEncoding is not null)
        {
            startInfo.StandardOutputEncoding = outputEncoding;
            startInfo.StandardErrorEncoding = outputEncoding;
        }
        if (environmentVariables is not null)
        {
            foreach (var (key, value) in environmentVariables)
            {
                startInfo.EnvironmentVariables[key] = value;
            }
        }
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        try
        {
            process.OutputDataReceived += (_, e) => OnLine(e.Data);
            process.ErrorDataReceived += (_, e) => OnLine(e.Data);

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);

            return new ProcessRunOutcome(true, null, process.ExitCode, outputLines);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            return new ProcessRunOutcome(false, "Operace byla zrušena.", null, outputLines);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return new ProcessRunOutcome(false, $"{fileName} se nepodařilo spustit: {ex.Message}", null, outputLines);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Proces mezitím sám skončil - nic k řešení.
        }
    }
}

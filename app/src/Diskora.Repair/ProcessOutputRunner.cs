using System.ComponentModel;
using System.Diagnostics;
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

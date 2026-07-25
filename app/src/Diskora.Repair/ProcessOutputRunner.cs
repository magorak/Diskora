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

        void OnLine(string? line)
        {
            if (line is null)
            {
                return;
            }

            outputLines.Add(line);
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

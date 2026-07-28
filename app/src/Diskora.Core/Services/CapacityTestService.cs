using Diskora.Core.Diagnostics;
using Diskora.Core.Models;

namespace Diskora.Core.Services;

public interface ICapacityTestService
{
    Task<CapacityTestResult> RunAsync(
        string driveLetter,
        IProgress<CapacityTestProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Ověří, že disk skutečně pojme a vrátí to, co tvrdí. Zaplní volné místo
/// souborem se vzorem odvozeným z pozice (<see cref="CapacityTestPattern"/>)
/// a přečte ho zpátky. Přeznačené flash disky - typicky levné USB klíčenky,
/// které hlásí 1 TB a fyzicky mají 32 GB - se prozradí tím, že přečtená data
/// nesedí, protože zápis nad skutečnou kapacitou přepsal dřívější obsah.
///
/// Záměrně pracuje se SOUBORY na svazku, ne se syrovým diskem:
/// - nepotřebuje práva administrátora,
/// - nezničí oddíly ani data, která na disku už jsou (píše jen do volného
///   místa a po sobě uklidí),
/// - funguje i na disku, který je zrovna používaný.
/// Cenou je, že netestuje část kapacity zabranou existujícími daty - pro
/// odhalení přeznačeného disku to ale stačí, protože ten se pozná i na zlomku
/// své deklarované velikosti.
/// </summary>
public sealed class CapacityTestService : ICapacityTestService
{
    /// <summary>Název složky, kterou test zakládá a po sobě zase maže.</summary>
    public const string TestFolderName = "diskora-test-kapacity";

    private const int BlockSize = 4 * 1024 * 1024;

    /// <summary>
    /// Kousek volného místa, který se nechává být. Zaplnit svazek úplně do
    /// posledního bajtu dělá problémy systému i uživateli.
    /// </summary>
    private const long FreeSpaceReserveBytes = 64L * 1024 * 1024;

    public async Task<CapacityTestResult> RunAsync(
        string driveLetter,
        IProgress<CapacityTestProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var root = Path.GetPathRoot(driveLetter.TrimEnd('\\', ':') + ":\\")
                   ?? throw new ArgumentException("Neplatné písmeno svazku.", nameof(driveLetter));
        var folder = Path.Combine(root, TestFolderName);
        var file = Path.Combine(folder, "vzorek.bin");

        long targetBytes;
        try
        {
            var drive = new DriveInfo(root);
            targetBytes = Math.Max(0, drive.AvailableFreeSpace - FreeSpaceReserveBytes);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return Failed($"Volné místo na svazku se nepodařilo zjistit: {ex.Message}");
        }

        if (targetBytes < BlockSize)
        {
            return Failed("Na svazku není dost volného místa (potřeba aspoň 68 MB) - test by neměl co zapsat.");
        }

        long written = 0;
        long verified = 0;

        try
        {
            Directory.CreateDirectory(folder);
            written = await WriteAsync(file, targetBytes, progress, cancellationToken).ConfigureAwait(false);
            var mismatch = await VerifyAsync(file, written, progress, cancellationToken).ConfigureAwait(false);
            verified = mismatch ?? written;

            return new CapacityTestResult(true, null, written, verified, mismatch);
        }
        catch (OperationCanceledException)
        {
            return new CapacityTestResult(false, "Test byl zrušen.", written, verified, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new CapacityTestResult(false, $"Test se nepodařilo dokončit: {ex.Message}", written, verified, null);
        }
        finally
        {
            // Uklidit se musí vždycky - i po zrušení nebo chybě by jinak na disku
            // zůstaly gigabajty testovacích dat.
            progress?.Report(new CapacityTestProgress(CapacityTestPhase.CleaningUp, written, written));
            TryDelete(folder);
        }
    }

    private static async Task<long> WriteAsync(
        string file, long targetBytes, IProgress<CapacityTestProgress>? progress, CancellationToken cancellationToken)
    {
        var buffer = new byte[BlockSize];
        long written = 0;

        await using var stream = new FileStream(
            file, FileMode.Create, FileAccess.Write, FileShare.None, BlockSize, useAsync: true);

        while (written < targetBytes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var length = (int)Math.Min(BlockSize, targetBytes - written);
            CapacityTestPattern.Fill(buffer.AsSpan(0, length), written);

            try
            {
                await stream.WriteAsync(buffer.AsMemory(0, length), cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                // Disk se zaplnil dřív, než sliboval - to je samo o sobě nález,
                // takže se s tím, co je zapsané, pokračuje na ověření.
                break;
            }

            written += length;
            progress?.Report(new CapacityTestProgress(CapacityTestPhase.Writing, written, targetBytes));
        }

        // Bez vyprázdnění na médium by se četlo z vyrovnávací paměti systému
        // a test by u přeznačeného disku prošel.
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        stream.Flush(flushToDisk: true);
        return written;
    }

    private static async Task<long?> VerifyAsync(
        string file, long writtenBytes, IProgress<CapacityTestProgress>? progress, CancellationToken cancellationToken)
    {
        var buffer = new byte[BlockSize];
        long position = 0;

        await using var stream = new FileStream(
            file, FileMode.Open, FileAccess.Read, FileShare.None, BlockSize, useAsync: true);

        while (position < writtenBytes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var length = (int)Math.Min(BlockSize, writtenBytes - position);
            var read = await stream.ReadAtLeastAsync(
                buffer.AsMemory(0, length), length, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);

            if (read < length)
            {
                // Přečetlo se míň, než se zapsalo - data chybí.
                return position + read;
            }

            if (CapacityTestPattern.FindFirstMismatch(buffer.AsSpan(0, length), position) is { } mismatch)
            {
                return mismatch;
            }

            position += length;
            progress?.Report(new CapacityTestProgress(CapacityTestPhase.Verifying, position, writtenBytes));
        }

        return null;
    }

    private static void TryDelete(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Úklid je best-effort; složku pozná uživatel podle názvu a smaže ji sám.
        }
    }

    private static CapacityTestResult Failed(string reason) => new(false, reason, 0, 0, null);
}

using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Diskora.Native.Storage;

/// <summary>
/// Needestruktivní povrchový sken fyzického disku: čte disk sekvenčně po
/// blocích a hlásí bajtové rozsahy, které se nepodařilo přečíst (I/O chyba
/// při čtení = pravděpodobně vadný sektor). Nic nezapisuje ani neopravuje -
/// na rozdíl od `chkdsk /f`/`/spotfix`, které jsou v Diskoře záměrně
/// samostatný, dosud nepropojený krok (viz TODO.md - potřebuje vlastní
/// potvrzovací UI kvůli riziku restartu na systémovém svazku).
///
/// Otevírá `\\.\PhysicalDriveN` napřímo (ne konkrétní svazek), proto
/// vyžaduje admin práva - stejné omezení jako u AttachVirtualDisk (viz
/// Diskora.VirtualDisks). Čte přes obyčejný bufferovaný FileStream, ne
/// FILE_FLAG_NO_BUFFERING - vyhnutí se zarovnávacím požadavkům přímého I/O
/// (adresa bufferu/délka/offset na hranici sektoru) za cenu menší režie
/// kopírování, což je pro needestruktivní sken v pořádku.
/// </summary>
public static class PhysicalDiskSurfaceScanner
{
    private const uint GenericRead = 0x80000000;
    private const uint FileShareRead = 0x1;
    private const uint FileShareWrite = 0x2;
    private const uint OpenExisting = 3;
    private const uint FileFlagSequentialScan = 0x08000000;

    private const int ErrorAccessDenied = 5;

    // 4 MiB bloky pro rychlý průchod celým diskem; při chybě se blok
    // rozpadne na jemnější 64 KiB rozsahy, aby hlášení bylo k něčemu.
    private const int BlockSize = 4 * 1024 * 1024;
    private const int FineBlockSize = 64 * 1024;

    public static async Task<NativeSurfaceScanResult> ScanAsync(
        int physicalDiskIndex,
        long sizeBytes,
        IProgress<long>? bytesScannedProgress,
        CancellationToken cancellationToken)
    {
        var path = $@"\\.\PhysicalDrive{physicalDiskIndex}";

        using var handle = CreateFile(
            path, GenericRead, FileShareRead | FileShareWrite,
            IntPtr.Zero, OpenExisting, FileFlagSequentialScan, IntPtr.Zero);

        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            return new NativeSurfaceScanResult(false, DescribeOpenFailure(error), 0, []);
        }

        using var stream = new FileStream(handle, FileAccess.Read);
        var badRanges = new List<NativeBadRange>();
        var buffer = new byte[BlockSize];
        long offset = 0;

        while (offset < sizeBytes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var blockLength = (int)Math.Min(BlockSize, sizeBytes - offset);
            var ok = await TryReadAsync(stream, offset, buffer, blockLength, cancellationToken).ConfigureAwait(false);

            if (!ok)
            {
                await FindBadRangesAsync(stream, offset, blockLength, badRanges, cancellationToken).ConfigureAwait(false);
            }

            offset += blockLength;
            bytesScannedProgress?.Report(offset);
        }

        return new NativeSurfaceScanResult(true, null, offset, badRanges);
    }

    private static async Task FindBadRangesAsync(
        FileStream stream, long blockOffset, int blockLength, List<NativeBadRange> badRanges, CancellationToken cancellationToken)
    {
        var fineBuffer = new byte[FineBlockSize];

        for (var sub = 0; sub < blockLength; sub += FineBlockSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var subLength = Math.Min(FineBlockSize, blockLength - sub);
            var ok = await TryReadAsync(stream, blockOffset + sub, fineBuffer, subLength, cancellationToken).ConfigureAwait(false);

            if (!ok)
            {
                badRanges.Add(new NativeBadRange(blockOffset + sub, subLength));
            }
        }
    }

    private static async Task<bool> TryReadAsync(FileStream stream, long offset, byte[] buffer, int length, CancellationToken cancellationToken)
    {
        try
        {
            stream.Seek(offset, SeekOrigin.Begin);

            var totalRead = 0;
            while (totalRead < length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(totalRead, length - totalRead), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break; // konec disku - není co dál číst
                }

                totalRead += read;
            }

            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static string DescribeOpenFailure(int win32Error) => win32Error == ErrorAccessDenied
        ? $"Povrchový sken se nepodařilo spustit - chybí práva administrátora (Win32 chyba {win32Error}). Spusťte Diskoru jako administrátor a zkuste to znovu."
        : $"Povrchový sken se nepodařilo spustit (Win32 chyba {win32Error}).";

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName, uint dwDesiredAccess, uint dwShareMode,
        IntPtr lpSecurityAttributes, uint dwCreationDisposition,
        uint dwFlagsAndAttributes, IntPtr hTemplateFile);
}

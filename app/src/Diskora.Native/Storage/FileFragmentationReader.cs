using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Diskora.Native.Storage;

/// <summary>
/// Zjišťuje počet fragmentů (nesouvislých rozsahů clusterů) souboru přes
/// FSCTL_GET_RETRIEVAL_POINTERS - na rozdíl od operací nad fyzickým diskem
/// (SMART, povrchový sken) stačí obyčejné právo číst daný soubor, žádná
/// elevace. Report před spuštěním defragmentace (Fáze 5) - needestruktivní,
/// nic nepřesouvá.
/// Buffer je dimenzovaný na max. <see cref="MaxTrackedExtents"/> - u souborů
/// s víc fragmenty se vrátí jen dolní odhad (<see cref="FileFragmentationReadResult.
/// ExtentCountIsLowerBound"/>=true) misto opakovaného volání IOCTL s posunutým
/// StartingVcn: extrémně fragmentovaný soubor je "hodně fragmentovaný" i bez
/// přesného čísla nad tuhle hranici, pro report to stačí.
/// </summary>
public static class FileFragmentationReader
{
    private const uint FsctlGetRetrievalPointers = 0x00090073;

    private const uint GenericRead = 0x80000000;
    private const uint FileShareRead = 0x1;
    private const uint FileShareWrite = 0x2;
    private const uint FileShareDelete = 0x4;
    private const uint OpenExisting = 3;

    private const int ErrorMoreData = 234;
    private const int ErrorHandleEof = 38;

    // RETRIEVAL_POINTERS_BUFFER: DWORD ExtentCount (4B) + zarovnávací mezera na
    // 8 bajtů (LARGE_INTEGER potřebuje 8B zarovnání) + LARGE_INTEGER StartingVcn (8B).
    private const int HeaderSize = 16;

    // Jeden Extents[] záznam: LARGE_INTEGER NextVcn (8B) + LARGE_INTEGER Lcn (8B).
    private const int ExtentEntrySize = 16;

    private const int MaxTrackedExtents = 512;

    public static FileFragmentationReadResult GetExtentCount(string path)
    {
        using var handle = CreateFile(
            path, GenericRead, FileShareRead | FileShareWrite | FileShareDelete,
            IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);

        if (handle.IsInvalid)
        {
            return new FileFragmentationReadResult(
                false, $"Soubor se nepodařilo otevřít (Win32 chyba {Marshal.GetLastWin32Error()}).", 0, false);
        }

        var input = new byte[8]; // STARTING_VCN_INPUT_BUFFER.StartingVcn = 0 (od začátku souboru)
        var output = new byte[HeaderSize + (MaxTrackedExtents * ExtentEntrySize)];

        var ok = DeviceIoControl(
            handle, FsctlGetRetrievalPointers,
            input, (uint)input.Length,
            output, (uint)output.Length,
            out _, IntPtr.Zero);

        if (ok)
        {
            return new FileFragmentationReadResult(true, null, BitConverter.ToInt32(output, 0), false);
        }

        var error = Marshal.GetLastWin32Error();
        return error switch
        {
            // Buffer je plný - do něj se vešlo MaxTrackedExtents záznamů, ale soubor
            // jich má víc.
            ErrorMoreData => new FileFragmentationReadResult(true, null, MaxTrackedExtents, true),
            // Soubor nemá žádné alokované clustery (prázdný, nebo rezidentní přímo v MFT) -
            // nula fragmentů, ne chyba.
            ErrorHandleEof => new FileFragmentationReadResult(true, null, 0, false),
            _ => new FileFragmentationReadResult(false, $"Rozvržení souboru se nepodařilo přečíst (Win32 chyba {error}).", 0, false),
        };
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName, uint dwDesiredAccess, uint dwShareMode,
        IntPtr lpSecurityAttributes, uint dwCreationDisposition,
        uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice, uint dwIoControlCode,
        byte[] lpInBuffer, uint nInBufferSize,
        byte[] lpOutBuffer, uint nOutBufferSize,
        out uint lpBytesReturned, IntPtr lpOverlapped);
}

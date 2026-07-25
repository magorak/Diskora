using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Diskora.Native.Fsctl;

/// <summary>
/// Zjišťuje "dirty bit" svazku přes FSCTL_IS_VOLUME_DIRTY - stejný
/// mechanismus, který Windows používá k rozhodnutí, zda při startu spustit
/// automatickou kontrolu disku. Čtení tohoto příznaku nevyžaduje práva
/// administrátora ani nijak nezasahuje do svazku.
/// </summary>
public static class VolumeDirtyChecker
{
    private const uint FsctlIsVolumeDirty = 0x00090078;
    private const uint VolumeIsDirtyFlag = 0x1;

    private const uint GenericRead = 0x80000000;
    private const uint FileShareRead = 0x1;
    private const uint FileShareWrite = 0x2;
    private const uint OpenExisting = 3;

    /// <summary>
    /// Vrací true/false pro zjištěný stav, nebo null pokud se stav nepodařilo
    /// zjistit (svazek nedostupný, neočekávaná chyba IOCTL apod.).
    /// </summary>
    public static bool? IsDirty(string driveLetter)
    {
        var path = $@"\\.\{driveLetter.TrimEnd('\\', ':')}:";

        using var handle = CreateFile(
            path, GenericRead, FileShareRead | FileShareWrite,
            IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);

        if (handle.IsInvalid)
        {
            return null;
        }

        var outBuffer = new byte[4];
        var ok = DeviceIoControl(
            handle, FsctlIsVolumeDirty,
            null, 0,
            outBuffer, (uint)outBuffer.Length,
            out _, IntPtr.Zero);

        if (!ok)
        {
            return null;
        }

        var flags = BitConverter.ToUInt32(outBuffer, 0);
        return (flags & VolumeIsDirtyFlag) != 0;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName, uint dwDesiredAccess, uint dwShareMode,
        IntPtr lpSecurityAttributes, uint dwCreationDisposition,
        uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice, uint dwIoControlCode,
        byte[]? lpInBuffer, uint nInBufferSize,
        byte[] lpOutBuffer, uint nOutBufferSize,
        out uint lpBytesReturned, IntPtr lpOverlapped);
}

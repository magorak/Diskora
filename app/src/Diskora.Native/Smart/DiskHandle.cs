using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Diskora.Native.Smart;

internal static class DiskHandle
{
    /// <summary>
    /// Otevře fyzický disk pro S.M.A.R.T. dotazy. Obě ATA cesty potřebují
    /// GENERIC_READ | GENERIC_WRITE (posílají disku příkaz, ne jen čtou
    /// vlastnost), a to znamená práva administrátora - na rozdíl od
    /// <see cref="NvmeHealthReader"/>, kterému stačí handle bez přístupových práv.
    /// </summary>
    public static SafeFileHandle? OpenForSmart(int physicalDriveIndex, out string? error)
    {
        var handle = NativeMethods.CreateFile(
            $@"\\.\PhysicalDrive{physicalDriveIndex}",
            NativeMethods.GenericRead | NativeMethods.GenericWrite,
            NativeMethods.FileShareRead | NativeMethods.FileShareWrite,
            IntPtr.Zero,
            NativeMethods.OpenExisting,
            0,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            error = $"Disk se nepodařilo otevřít (Win32 chyba {Marshal.GetLastWin32Error()}). Zkuste spustit Diskoru jako administrátor.";
            handle.Dispose();
            return null;
        }

        error = null;
        return handle;
    }
}

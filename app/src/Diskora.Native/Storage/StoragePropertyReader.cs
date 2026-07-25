using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Diskora.Native.Storage;

/// <summary>
/// Zjišťuje, zda disk má "seek penalty" (rotační HDD) a zda podporuje TRIM,
/// přes IOCTL_STORAGE_QUERY_PROPERTY otevřený na SVAZKU (ne fyzickém disku) -
/// to funguje bez práv administrátora na běžných svazcích (na systémovém/boot
/// svazku C: vyžaduje elevaci stejně jako FSCTL_IS_VOLUME_DIRTY, viz
/// VolumeDirtyChecker - ověřeno empiricky).
/// </summary>
public static class StoragePropertyReader
{
    private const uint IoctlStorageQueryProperty = 0x002D1400;
    private const uint PropertyIdDeviceSeekPenalty = 7;
    private const uint PropertyIdDeviceTrim = 8;

    private const uint GenericRead = 0x80000000;
    private const uint FileShareRead = 0x1;
    private const uint FileShareWrite = 0x2;
    private const uint OpenExisting = 3;

    /// <summary>True = má seek penalty (rotační HDD). Null = nepodařilo se zjistit.</summary>
    public static bool? HasSeekPenalty(string driveLetter) => QueryFlag(driveLetter, PropertyIdDeviceSeekPenalty);

    /// <summary>True = disk podporuje TRIM. Null = nepodařilo se zjistit.</summary>
    public static bool? SupportsTrim(string driveLetter) => QueryFlag(driveLetter, PropertyIdDeviceTrim);

    private static bool? QueryFlag(string driveLetter, uint propertyId)
    {
        var path = $@"\\.\{driveLetter.TrimEnd('\\', ':')}:";

        using var handle = CreateFile(
            path, GenericRead, FileShareRead | FileShareWrite,
            IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);

        if (handle.IsInvalid)
        {
            return null;
        }

        // STORAGE_PROPERTY_QUERY: PropertyId(4) + QueryType(4) + AdditionalParameters(padded, 4) = 12 bajtů.
        var query = new byte[12];
        BitConverter.GetBytes(propertyId).CopyTo(query, 0);
        BitConverter.GetBytes(0u).CopyTo(query, 4); // PropertyStandardQuery

        // DEVICE_SEEK_PENALTY_DESCRIPTOR / DEVICE_TRIM_DESCRIPTOR: Version(4) + Size(4) + flag(1, paddováno).
        var output = new byte[16];

        var ok = DeviceIoControl(
            handle, IoctlStorageQueryProperty,
            query, (uint)query.Length,
            output, (uint)output.Length,
            out _, IntPtr.Zero);

        return ok ? output[8] != 0 : null;
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

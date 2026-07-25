using System.Runtime.InteropServices;

namespace Diskora.VirtualDisks.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct VirtualStorageType
{
    public uint DeviceId;
    public Guid VendorId;
}

/// <summary>
/// Nízkoúrovňové P/Invoke deklarace pro virtdisk.dll. Rozložení struktur
/// (zejména offsety uvnitř unionů v OPEN_VIRTUAL_DISK_PARAMETERS a
/// GET_VIRTUAL_DISK_INFO) je ověřené empiricky proti reálnému VHDX souboru,
/// ne jen opsané z dokumentace - viz komentáře u konkrétních offsetů.
/// </summary>
internal static class VirtDiskNativeMethods
{
    [DllImport("virtdisk.dll", CharSet = CharSet.Unicode)]
    public static extern int OpenVirtualDisk(
        ref VirtualStorageType virtualStorageType,
        string path,
        int virtualDiskAccessMask,
        int flags,
        IntPtr parameters,
        out IntPtr handle);

    [DllImport("virtdisk.dll")]
    public static extern int GetVirtualDiskInformation(
        IntPtr virtualDiskHandle,
        ref uint virtualDiskInfoSize,
        IntPtr virtualDiskInfo,
        IntPtr sizeUsed);

    [DllImport("virtdisk.dll")]
    public static extern int AttachVirtualDisk(
        IntPtr virtualDiskHandle,
        IntPtr securityDescriptor,
        int flags,
        uint providerSpecificFlags,
        IntPtr parameters,
        IntPtr overlapped);

    [DllImport("virtdisk.dll")]
    public static extern int DetachVirtualDisk(
        IntPtr virtualDiskHandle,
        int flags,
        uint providerSpecificFlags);

    [DllImport("kernel32.dll")]
    public static extern bool CloseHandle(IntPtr handle);

    /// <summary>
    /// OPEN_VIRTUAL_DISK_PARAMETERS, verze 2 (28 bajtů):
    /// offset 0 Version(4)=2, offset 4 GetInfoOnly(4), offset 8 ReadOnly(4),
    /// offset 12 ResiliencyGuid(16). Žádné skryté zarovnání - Version i
    /// union členy jsou 4bajtově zarovnané. Ověřeno funkční jen pro
    /// GetInfoOnly=TRUE + přístupová maska 0 (viz <see cref="BuildOpenParametersV1"/>
    /// pro připojení, kde V2 s nenulovou maskou empiricky vracelo
    /// ERROR_INVALID_PARAMETER).
    /// </summary>
    public static byte[] BuildOpenParametersV2(bool getInfoOnly, bool readOnly)
    {
        var buffer = new byte[28];
        BitConverter.GetBytes(2).CopyTo(buffer, 0);
        BitConverter.GetBytes(getInfoOnly ? 1 : 0).CopyTo(buffer, 4);
        BitConverter.GetBytes(readOnly ? 1 : 0).CopyTo(buffer, 8);
        return buffer;
    }

    /// <summary>
    /// OPEN_VIRTUAL_DISK_PARAMETERS, verze 1 (8 bajtů): Version(4)=1 + RWDepth(4).
    /// Použít při otevírání s reálnou přístupovou maskou (attach/detach) -
    /// verze 2 v kombinaci s nenulovou maskou empiricky selhávala
    /// s ERROR_INVALID_PARAMETER (87), i když je dle dokumentace validní
    /// kombinace - ověřeno proti reálnému VHDX.
    /// </summary>
    public static byte[] BuildOpenParametersV1(uint rwDepth = 1)
    {
        var buffer = new byte[8];
        BitConverter.GetBytes(1).CopyTo(buffer, 0);
        BitConverter.GetBytes(rwDepth).CopyTo(buffer, 4);
        return buffer;
    }

    /// <summary>
    /// GET_VIRTUAL_DISK_INFO s Version=GET_VIRTUAL_DISK_INFO_SIZE (1).
    /// Union obsahuje ULONGLONG členy (8bajtové zarovnání), proto je mezi
    /// Version (offset 0) a daty 4 bajty výplně - data začínají a offsetu 8,
    /// ne 4. Ověřeno empiricky, ne jen podle dokumentace.
    /// Offsety: VirtualSize=8 (u64), PhysicalSize=16 (u64), BlockSize=24 (u32),
    /// SectorSize=28 (u32).
    /// </summary>
    public static byte[] BuildSizeInfoRequestBuffer()
    {
        var buffer = new byte[32];
        BitConverter.GetBytes(1).CopyTo(buffer, 0); // GET_VIRTUAL_DISK_INFO_SIZE
        return buffer;
    }

    /// <summary>ATTACH_VIRTUAL_DISK_PARAMETERS, verze 1 (8 bajtů): Version(4) + Reserved(4).</summary>
    public static byte[] BuildAttachParametersV1()
    {
        var buffer = new byte[8];
        BitConverter.GetBytes(1).CopyTo(buffer, 0);
        return buffer;
    }
}

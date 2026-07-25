using System.Runtime.InteropServices;
using Diskora.VirtualDisks.Interop;

namespace Diskora.VirtualDisks;

/// <summary>
/// Čte metadata VHD/VHDX (velikost, blok, sektor) přes virtdisk.dll s
/// GetInfoOnly=TRUE - to funguje i bez práv administrátora, protože se
/// nejedná o připojení disku, jen o čtení hlavičky souboru. Připojení
/// (mount) samotné vyžaduje elevaci - viz <see cref="VirtualDiskAttacher"/>.
///
/// ISO obrazy jsou zvláštní případ: `GetVirtualDiskInformation` s
/// GetInfoOnly=TRUE pro ně empiricky vrací ERROR_INVALID_PARAMETER (87)
/// bez ohledu na DeviceId (auto i explicitní ISO) - virtdisk.dll tuhle
/// cestu pro ISO poskytovatele nepodporuje. Velikost se proto čte přímo
/// ze souboru; sektor je pevně 2048 B (standard ISO 9660/CD-ROM), pojem
/// "blok" u ISO nedává smysl (žádné dynamické růstové bloky jako u VHDX).
/// </summary>
public static class VirtualDiskReader
{
    private const uint IsoSectorSize = 2048;

    public static VirtualDiskReadResult GetInfo(string path)
    {
        if (DetectFormat(path) == VirtualDiskFormat.Iso)
        {
            return GetIsoInfo(path);
        }

        var storageType = default(VirtualStorageType); // DeviceId=0, VendorId=Guid.Empty => auto-detekce dle přípony
        var openParams = VirtDiskNativeMethods.BuildOpenParametersV2(getInfoOnly: true, readOnly: true);
        var openParamsHandle = GCHandle.Alloc(openParams, GCHandleType.Pinned);

        try
        {
            var openResult = VirtDiskNativeMethods.OpenVirtualDisk(
                ref storageType,
                path,
                virtualDiskAccessMask: 0,
                flags: 0,
                openParamsHandle.AddrOfPinnedObject(),
                out var handle);

            if (openResult != 0)
            {
                return Failure($"Soubor se nepodařilo otevřít (Win32 chyba {openResult}).");
            }

            try
            {
                return ReadSizeInfo(handle, path);
            }
            finally
            {
                VirtDiskNativeMethods.CloseHandle(handle);
            }
        }
        finally
        {
            openParamsHandle.Free();
        }
    }

    private static VirtualDiskReadResult GetIsoInfo(string path)
    {
        try
        {
            var size = (ulong)new FileInfo(path).Length;
            var info = new VirtualDiskInfo(path, VirtualDiskFormat.Iso, size, size, IsoSectorSize, IsoSectorSize);
            return new VirtualDiskReadResult(true, null, info);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Failure($"Soubor se nepodařilo přečíst ({ex.Message}).");
        }
    }

    private static VirtualDiskReadResult ReadSizeInfo(IntPtr handle, string path)
    {
        var infoBuffer = VirtDiskNativeMethods.BuildSizeInfoRequestBuffer();
        var infoSize = (uint)infoBuffer.Length;
        var infoHandle = GCHandle.Alloc(infoBuffer, GCHandleType.Pinned);

        try
        {
            var result = VirtDiskNativeMethods.GetVirtualDiskInformation(
                handle, ref infoSize, infoHandle.AddrOfPinnedObject(), IntPtr.Zero);

            if (result != 0)
            {
                return Failure($"Metadata disku se nepodařilo přečíst (Win32 chyba {result}).");
            }

            var virtualSize = BitConverter.ToUInt64(infoBuffer, 8);
            var physicalSize = BitConverter.ToUInt64(infoBuffer, 16);
            var blockSize = BitConverter.ToUInt32(infoBuffer, 24);
            var sectorSize = BitConverter.ToUInt32(infoBuffer, 28);

            var info = new VirtualDiskInfo(path, DetectFormat(path), virtualSize, physicalSize, blockSize, sectorSize);
            return new VirtualDiskReadResult(true, null, info);
        }
        finally
        {
            infoHandle.Free();
        }
    }

    private static VirtualDiskFormat DetectFormat(string path) => Path.GetExtension(path).ToUpperInvariant() switch
    {
        ".VHD" => VirtualDiskFormat.Vhd,
        ".VHDX" => VirtualDiskFormat.Vhdx,
        ".ISO" => VirtualDiskFormat.Iso,
        _ => VirtualDiskFormat.Unknown,
    };

    private static VirtualDiskReadResult Failure(string reason) => new(false, reason, null);
}

using System.Runtime.InteropServices;
using Diskora.VirtualDisks.Interop;

namespace Diskora.VirtualDisks;

/// <summary>
/// Čte metadata VHD/VHDX (velikost, blok, sektor) přes virtdisk.dll s
/// GetInfoOnly=TRUE - to funguje i bez práv administrátora, protože se
/// nejedná o připojení disku, jen o čtení hlavičky souboru. Připojení
/// (mount) samotné vyžaduje elevaci - viz <see cref="VirtualDiskAttacher"/>.
/// </summary>
public static class VirtualDiskReader
{
    public static VirtualDiskReadResult GetInfo(string path)
    {
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
        _ => VirtualDiskFormat.Unknown,
    };

    private static VirtualDiskReadResult Failure(string reason) => new(false, reason, null);
}

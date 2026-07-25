using System.Management;
using Diskora.Core.Models;

namespace Diskora.Core.Services;

/// <summary>
/// Čte fyzické disky přes moderní Storage WMI poskytovatele (MSFT_PhysicalDisk),
/// který dává typ média (HDD/SSD) a sběrnici. Pokud poskytovatel není dostupný
/// (starší Windows, omezené prostředí), spadne zpět na Win32_DiskDrive, který
/// typ média spolehlivě nerozliší, ale je dostupný univerzálně.
/// </summary>
public sealed class DiskEnumerationService : IDiskEnumerationService
{
    public IReadOnlyList<PhysicalDiskInfo> GetPhysicalDisks()
    {
        try
        {
            return GetPhysicalDisksFromStorageProvider();
        }
        catch (ManagementException)
        {
            return GetPhysicalDisksFallback();
        }
        catch (UnauthorizedAccessException)
        {
            return GetPhysicalDisksFallback();
        }
    }

    public IReadOnlyList<VolumeInfo> GetVolumes()
    {
        var volumes = new List<VolumeInfo>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady)
            {
                continue;
            }

            try
            {
                volumes.Add(new VolumeInfo(
                    Name: drive.Name,
                    Label: SafeGetLabel(drive),
                    FileSystem: SafeGetFileSystem(drive),
                    TotalSizeBytes: drive.TotalSize,
                    FreeSpaceBytes: drive.AvailableFreeSpace,
                    DriveType: drive.DriveType,
                    PhysicalDiskIndex: TryGetPhysicalDiskIndex(drive.Name)));
            }
            catch (IOException)
            {
                // Svazek zmizel/nebyl čitelný mezi IsReady kontrolou a čtením vlastností
                // (např. vyjmuté vyměnitelné médium) - přeskočí se.
            }
        }

        return volumes;
    }

    /// <summary>
    /// Zjistí, na kterém fyzickém disku (Win32_DiskDrive.Index) leží daný svazek,
    /// přes standardní WMI asociátorový řetězec Win32_LogicalDisk →
    /// Win32_LogicalDiskToPartition → Win32_DiskPartition →
    /// Win32_DiskDriveToDiskPartition → Win32_DiskDrive - ověřeno živě.
    /// U svazků rozložených přes více disků (Storage Spaces apod.) se vrátí
    /// první nalezený disk - přesnější mapování 1:N zatím Diskora nepotřebuje.
    /// Vrací null pro svazky bez mapování (síťové jednotky apod.).
    /// </summary>
    private static int? TryGetPhysicalDiskIndex(string driveName)
    {
        var deviceId = driveName.TrimEnd('\\');

        try
        {
            using var partitionSearcher = new ManagementObjectSearcher(
                $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{deviceId}'}} WHERE AssocClass = Win32_LogicalDiskToPartition");

            foreach (ManagementBaseObject partitionItem in partitionSearcher.Get())
            {
                using var partition = partitionItem;
                if (partition["DeviceID"] is not string partitionDeviceId)
                {
                    continue;
                }

                using var diskSearcher = new ManagementObjectSearcher(
                    $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partitionDeviceId}'}} WHERE AssocClass = Win32_DiskDriveToDiskPartition");

                foreach (ManagementBaseObject diskItem in diskSearcher.Get())
                {
                    using var disk = diskItem;
                    return Convert.ToInt32(disk["Index"]);
                }
            }
        }
        catch (ManagementException)
        {
            // Svazek nemusí mít mapování na fyzický disk (síťová jednotka apod.) - v pořádku.
        }

        return null;
    }

    private static string? SafeGetLabel(DriveInfo drive)
    {
        try
        {
            return drive.VolumeLabel;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static string? SafeGetFileSystem(DriveInfo drive)
    {
        try
        {
            return drive.DriveFormat;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static IReadOnlyList<PhysicalDiskInfo> GetPhysicalDisksFromStorageProvider()
    {
        var disks = new List<PhysicalDiskInfo>();

        using var searcher = new ManagementObjectSearcher(
            @"root\Microsoft\Windows\Storage",
            "SELECT DeviceId, FriendlyName, Size, MediaType, BusType, SerialNumber FROM MSFT_PhysicalDisk");

        foreach (ManagementBaseObject item in searcher.Get())
        {
            using var disk = item;
            disks.Add(new PhysicalDiskInfo(
                Index: int.Parse((string)disk["DeviceId"]),
                FriendlyName: disk["FriendlyName"] as string ?? "Neznámý disk",
                SizeBytes: Convert.ToUInt64(disk["Size"]),
                MediaType: MapMediaType(Convert.ToUInt16(disk["MediaType"])),
                BusType: MapBusType(Convert.ToUInt16(disk["BusType"])),
                SerialNumber: (disk["SerialNumber"] as string)?.Trim()));
        }

        return disks;
    }

    private static IReadOnlyList<PhysicalDiskInfo> GetPhysicalDisksFallback()
    {
        var disks = new List<PhysicalDiskInfo>();

        using var searcher = new ManagementObjectSearcher(
            "SELECT Index, Model, Size, InterfaceType, MediaType, SerialNumber FROM Win32_DiskDrive");

        foreach (ManagementBaseObject item in searcher.Get())
        {
            using var disk = item;
            var mediaTypeText = disk["MediaType"] as string ?? string.Empty;

            disks.Add(new PhysicalDiskInfo(
                Index: Convert.ToInt32(disk["Index"]),
                FriendlyName: disk["Model"] as string ?? "Neznámý disk",
                SizeBytes: disk["Size"] is null ? 0UL : Convert.ToUInt64(disk["Size"]),
                MediaType: mediaTypeText.Contains("removable", StringComparison.OrdinalIgnoreCase)
                    ? DiskMediaType.Removable
                    : DiskMediaType.Unknown,
                BusType: MapInterfaceType(disk["InterfaceType"] as string),
                SerialNumber: (disk["SerialNumber"] as string)?.Trim()));
        }

        return disks;
    }

    private static DiskMediaType MapMediaType(ushort mediaType) => mediaType switch
    {
        3 => DiskMediaType.HardDisk,
        4 => DiskMediaType.SolidState,
        5 => DiskMediaType.StorageClassMemory,
        _ => DiskMediaType.Unknown,
    };

    private static DiskBusType MapBusType(ushort busType) => busType switch
    {
        1 => DiskBusType.Scsi,
        2 => DiskBusType.Atapi,
        3 => DiskBusType.Ata,
        4 => DiskBusType.Ieee1394,
        5 => DiskBusType.Ssa,
        6 => DiskBusType.FibreChannel,
        7 => DiskBusType.Usb,
        8 => DiskBusType.Raid,
        9 => DiskBusType.Iscsi,
        10 => DiskBusType.Sas,
        11 => DiskBusType.Sata,
        12 => DiskBusType.Sd,
        13 => DiskBusType.Mmc,
        14 => DiskBusType.Virtual,
        15 => DiskBusType.FileBackedVirtual,
        16 => DiskBusType.StorageSpaces,
        17 => DiskBusType.Nvme,
        18 => DiskBusType.StorageClassMemory,
        19 => DiskBusType.Ufs,
        _ => DiskBusType.Unknown,
    };

    private static DiskBusType MapInterfaceType(string? interfaceType) => interfaceType?.ToUpperInvariant() switch
    {
        "USB" => DiskBusType.Usb,
        "IDE" => DiskBusType.Ata,
        "SCSI" => DiskBusType.Scsi,
        "1394" => DiskBusType.Ieee1394,
        _ => DiskBusType.Unknown,
    };
}

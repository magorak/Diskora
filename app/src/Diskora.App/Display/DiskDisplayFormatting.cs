using System.IO;
using Diskora.Core.Models;

namespace Diskora.App.Display;

/// <summary>
/// Převod doménových enumů z Diskora.Core na česká zobrazovaná jména.
/// Drženo v App vrstvě, aby Core zůstal nezávislý na jazyce UI (viz Fáze 8 -
/// lokalizace bude tuto vrstvu nahrazovat resource soubory).
/// </summary>
public static class DiskDisplayFormatting
{
    public static string ToDisplayText(this DiskMediaType mediaType) => mediaType switch
    {
        DiskMediaType.HardDisk => "HDD",
        DiskMediaType.SolidState => "SSD",
        DiskMediaType.StorageClassMemory => "SCM",
        DiskMediaType.Removable => "Vyměnitelný",
        DiskMediaType.Virtual => "Virtuální",
        _ => "Neznámý",
    };

    public static string ToDisplayText(this DiskBusType busType) => busType switch
    {
        DiskBusType.Nvme => "NVMe",
        DiskBusType.Sata => "SATA",
        DiskBusType.Ata => "ATA/IDE",
        DiskBusType.Atapi => "ATAPI",
        DiskBusType.Usb => "USB",
        DiskBusType.Scsi => "SCSI",
        DiskBusType.Sas => "SAS",
        DiskBusType.Raid => "RAID",
        DiskBusType.Iscsi => "iSCSI",
        DiskBusType.Sd => "SD",
        DiskBusType.Mmc => "MMC",
        DiskBusType.Ieee1394 => "FireWire",
        DiskBusType.FibreChannel => "Fibre Channel",
        DiskBusType.Ssa => "SSA",
        DiskBusType.Virtual => "Virtuální",
        DiskBusType.FileBackedVirtual => "Virtuální (soubor)",
        DiskBusType.StorageSpaces => "Storage Spaces",
        DiskBusType.StorageClassMemory => "SCM",
        DiskBusType.Ufs => "UFS",
        _ => "Neznámé",
    };

    public static string ToDisplayText(this DriveType driveType) => driveType switch
    {
        DriveType.Fixed => "Pevný disk",
        DriveType.Removable => "Vyměnitelný",
        DriveType.Network => "Síťový",
        DriveType.CDRom => "CD/DVD",
        DriveType.Ram => "RAM disk",
        _ => "Neznámý",
    };
}

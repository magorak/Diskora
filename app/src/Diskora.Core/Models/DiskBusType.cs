namespace Diskora.Core.Models;

/// <summary>
/// Odpovídá hodnotám BusType z WMI MSFT_PhysicalDisk / Win32_DiskDrive.InterfaceType.
/// </summary>
public enum DiskBusType
{
    Unknown,
    Scsi,
    Atapi,
    Ata,
    Ieee1394,
    Ssa,
    FibreChannel,
    Usb,
    Raid,
    Iscsi,
    Sas,
    Sata,
    Sd,
    Mmc,
    Virtual,
    FileBackedVirtual,
    StorageSpaces,
    Nvme,
    StorageClassMemory,
    Ufs,
}

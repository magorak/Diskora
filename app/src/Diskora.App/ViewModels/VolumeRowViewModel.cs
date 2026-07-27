using Diskora.App.Display;
using Diskora.Core.Formatting;
using Diskora.Core.Models;

namespace Diskora.App.ViewModels;

public sealed class VolumeRowViewModel
{
    public VolumeRowViewModel(VolumeInfo info, PhysicalDiskInfo? physicalDisk)
    {
        var diskMediaType = physicalDisk?.MediaType;
        PhysicalDiskIndex = info.PhysicalDiskIndex;
        PhysicalDiskName = physicalDisk?.FriendlyName ?? "(neznámý disk)";
        PhysicalDiskSizeBytes = (long)(physicalDisk?.SizeBytes ?? 0);

        Name = info.Name;
        Label = string.IsNullOrWhiteSpace(info.Label) ? "(bez názvu)" : info.Label;
        FileSystem = info.FileSystem ?? "—";
        DriveTypeDisplay = info.DriveType.ToDisplayText();
        TotalSizeDisplay = ByteSizeFormatter.Format(info.TotalSizeBytes);
        FreeSpaceDisplay = ByteSizeFormatter.Format(info.FreeSpaceBytes);
        DiskMediaType = diskMediaType;
        DiskMediaTypeDisplay = diskMediaType?.ToDisplayText() ?? "—";

        var usedBytes = info.TotalSizeBytes - info.FreeSpaceBytes;
        UsedPercent = info.TotalSizeBytes > 0
            ? Math.Clamp(usedBytes * 100.0 / info.TotalSizeBytes, 0, 100)
            : 0;
    }

    public string Name { get; }

    public string Label { get; }

    public string FileSystem { get; }

    public string DriveTypeDisplay { get; }

    public string TotalSizeDisplay { get; }

    public string FreeSpaceDisplay { get; }

    public double UsedPercent { get; }

    /// <summary>Typ média fyzického disku pod tímto svazkem, pokud se ho podařilo zjistit.</summary>
    public DiskMediaType? DiskMediaType { get; }

    public string DiskMediaTypeDisplay { get; }

    /// <summary>Fyzický disk pod svazkem - null, když se mapování nepodařilo (svazek přes víc disků, síťová jednotka).</summary>
    public int? PhysicalDiskIndex { get; }

    public string PhysicalDiskName { get; }

    public long PhysicalDiskSizeBytes { get; }
}

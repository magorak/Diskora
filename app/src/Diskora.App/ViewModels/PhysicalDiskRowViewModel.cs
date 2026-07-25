using Diskora.App.Display;
using Diskora.Core.Formatting;
using Diskora.Core.Models;

namespace Diskora.App.ViewModels;

public sealed class PhysicalDiskRowViewModel(PhysicalDiskInfo info)
{
    public int Index { get; } = info.Index;

    public string FriendlyName { get; } = info.FriendlyName;

    public string SizeDisplay { get; } = ByteSizeFormatter.Format((long)info.SizeBytes);

    public string MediaTypeDisplay { get; } = info.MediaType.ToDisplayText();

    public string BusTypeDisplay { get; } = info.BusType.ToDisplayText();

    public string SerialNumberDisplay { get; } = string.IsNullOrWhiteSpace(info.SerialNumber)
        ? "—"
        : info.SerialNumber;
}

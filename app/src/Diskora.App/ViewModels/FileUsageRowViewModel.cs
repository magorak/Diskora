using Diskora.Core.Formatting;
using Diskora.Core.Models;

namespace Diskora.App.ViewModels;

public sealed class FileUsageRowViewModel(FileUsageEntry entry)
{
    public string Name { get; } = entry.Name;

    public string FullPath { get; } = entry.FullPath;

    public long SizeBytes { get; } = entry.SizeBytes;

    public string SizeDisplay { get; } = ByteSizeFormatter.Format(entry.SizeBytes);

    public string LastWriteDisplay { get; } = entry.LastWriteTimeUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
}

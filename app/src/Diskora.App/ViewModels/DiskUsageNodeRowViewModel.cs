using Diskora.Core.Formatting;
using Diskora.Core.Models;

namespace Diskora.App.ViewModels;

public sealed class DiskUsageNodeRowViewModel(DirectoryUsageNode node, long parentSizeBytes)
{
    public DirectoryUsageNode Node { get; } = node;

    public string Name { get; } = node.Name;

    public string SizeDisplay { get; } = ByteSizeFormatter.Format(node.SizeBytes);

    public double PercentOfParent { get; } = parentSizeBytes > 0
        ? Math.Clamp(node.SizeBytes * 100.0 / parentSizeBytes, 0, 100)
        : 0;

    public int FileCount { get; } = node.FileCount;

    public bool HasChildren { get; } = node.Subdirectories.Count > 0;

    public bool HadAccessError { get; } = node.HadAccessError;

    public bool IsReparsePoint { get; } = node.IsReparsePoint;

    public string StatusDisplay => HadAccessError
        ? "Přístup odepřen"
        : IsReparsePoint
            ? "Odkaz (nerozbaleno)"
            : string.Empty;

    public bool CanNavigateInto => HasChildren && !HadAccessError && !IsReparsePoint;
}

using Diskora.Core.Formatting;
using Diskora.Core.Models;

namespace Diskora.App.ViewModels;

public sealed class FragmentedFileRowViewModel(FragmentedFileEntry entry)
{
    public string FullPath { get; } = entry.FullPath;

    public string SizeDisplay { get; } = ByteSizeFormatter.Format(entry.SizeBytes);

    public string FragmentCountDisplay { get; } = entry.FragmentCountIsLowerBound
        ? $"{entry.FragmentCount}+"
        : entry.FragmentCount.ToString();
}

using Diskora.Core.Formatting;

namespace Diskora.App.ViewModels;

public sealed class DuplicateFileRowViewModel(int groupNumber, long sizeBytes, string fullPath)
{
    public int GroupNumber { get; } = groupNumber;

    public long SizeBytes { get; } = sizeBytes;

    public string SizeDisplay { get; } = ByteSizeFormatter.Format(sizeBytes);

    public string FullPath { get; } = fullPath;
}

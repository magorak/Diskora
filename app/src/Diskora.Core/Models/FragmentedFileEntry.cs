namespace Diskora.Core.Models;

public sealed record FragmentedFileEntry(string FullPath, long SizeBytes, int FragmentCount, bool FragmentCountIsLowerBound);

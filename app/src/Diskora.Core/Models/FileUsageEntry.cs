namespace Diskora.Core.Models;

public sealed record FileUsageEntry(string FullPath, string Name, long SizeBytes, DateTime LastWriteTimeUtc);

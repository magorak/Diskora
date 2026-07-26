namespace Diskora.Core.Models;

/// <summary>Záznam o virtuálním disku/obrazu, který Diskora připojila a dosud přes ni neodpojila.</summary>
public sealed record AttachedVirtualDiskEntry(string Path, VirtualDiskFormat Format, bool ReadOnly, DateTimeOffset AttachedAtUtc);

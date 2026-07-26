namespace Diskora.Core.Models;

/// <summary>
/// Skupina souborů se shodným obsahem (ověřeno hashem SHA-256, ne jen
/// velikostí). <see cref="FilePaths"/> má vždy alespoň 2 položky.
/// </summary>
public sealed record DuplicateFileGroup(long SizeBytes, IReadOnlyList<string> FilePaths)
{
    /// <summary>Kolik bajtů by šlo uvolnit smazáním všech kopií kromě jedné.</summary>
    public long ReclaimableBytes => SizeBytes * (FilePaths.Count - 1);
}

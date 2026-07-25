namespace Diskora.Core.Models;

/// <summary>
/// Uzel stromu zaplněnosti disku (styl TreeSize/WinDirStat). Velikost
/// zahrnuje rekurzivně všechny potomky. Body pouhé listy filesystému
/// (junction/symlink cíle) se nesledují, aby nedošlo k nekonečné rekurzi
/// nebo dvojímu započítání - takový uzel má <see cref="IsReparsePoint"/>.
/// </summary>
public sealed class DirectoryUsageNode
{
    public required string Name { get; init; }

    public required string FullPath { get; init; }

    public long SizeBytes { get; set; }

    public int FileCount { get; set; }

    public bool HadAccessError { get; set; }

    public bool IsReparsePoint { get; set; }

    public List<DirectoryUsageNode> Subdirectories { get; } = [];
}

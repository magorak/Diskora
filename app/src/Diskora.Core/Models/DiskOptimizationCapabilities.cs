namespace Diskora.Core.Models;

/// <summary>
/// HasSeekPenalty/SupportsTrim jsou null, když se nepodařilo zjistit (typicky
/// chybějící oprávnění na systémovém/boot svazku). IsLikelySolidState se
/// používá k rozhodnutí, jestli nabídnout TRIM (SSD) nebo defragmentaci (HDD) -
/// při nejistotě (null) se v UI nenabízí ani jedno, aby se nenabádalo
/// k nesprávné akci.
/// </summary>
public sealed record DiskOptimizationCapabilities(bool? HasSeekPenalty, bool? SupportsTrim)
{
    public bool? IsLikelySolidState => HasSeekPenalty is null ? null : !HasSeekPenalty;
}

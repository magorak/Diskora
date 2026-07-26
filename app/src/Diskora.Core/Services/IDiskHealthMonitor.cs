using Diskora.Core.Models;

namespace Diskora.Core.Services;

/// <summary>Periodicky volatelná kontrola zhoršení zdraví disků - podklad pro tray upozornění.</summary>
public interface IDiskHealthMonitor
{
    /// <summary>
    /// Pro každý index přečte aktuální S.M.A.R.T. zdraví (přes <see cref="ISmartService"/>,
    /// což zároveň zapíše nový záznam do historie) a porovná ho s posledním záznamem
    /// PŘED tímto čtením. Disky, kde SMART není dostupné, se tiše přeskočí.
    /// </summary>
    IReadOnlyList<DiskHealthChangeResult> CheckForDegradation(IReadOnlyList<int> diskIndexes);
}

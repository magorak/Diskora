using Diskora.Core.Models;

namespace Diskora.Core.Services;

/// <summary>
/// Rozhoduje, jestli si zhoršení zdraví disku zaslouží upozornit uživatele. Čistá
/// funkce bez závislostí, aby šla otestovat bez SQLite/nativního SMART čtení.
/// </summary>
public static class DiskHealthChangeDetector
{
    /// <summary>
    /// Zhoršení = předchozí stav je znám (ne null, ne Unknown), nový stav taky není
    /// Unknown, a nový stav je horší (vyšší v pořadí Healthy &lt; Warning &lt; Critical).
    /// První kontrola disku (žádná historie) i degradace do/z Unknown se záměrně
    /// nehlásí - Unknown typicky znamená "SMART teď nejde přečíst", ne že se disk
    /// zhoršil, a bez historie není s čím srovnávat.
    /// </summary>
    public static bool HasDegraded(DiskHealthStatus? previous, DiskHealthStatus current)
    {
        if (previous is null || previous == DiskHealthStatus.Unknown || current == DiskHealthStatus.Unknown)
        {
            return false;
        }

        return current > previous;
    }
}

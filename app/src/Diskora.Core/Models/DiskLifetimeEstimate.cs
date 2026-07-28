using System.Globalization;

namespace Diskora.Core.Models;

/// <summary>
/// Odhad zbývající životnosti disku. Když se odhadnout nedá, je
/// <paramref name="IsAvailable"/> false a <paramref name="UnavailableReason"/>
/// vysvětluje proč - vymyšlené číslo by bylo horší než žádné.
/// </summary>
public sealed record DiskLifetimeEstimate(
    bool IsAvailable,
    string? UnavailableReason,
    double? WearPercent,
    double? PowerOnHours,
    TimeSpan? RemainingTime)
{
    public static DiskLifetimeEstimate Unavailable(string reason) => new(false, reason, null, null, null);

    /// <summary>Věta pro uživatele - buď odhad, nebo důvod, proč žádný není.</summary>
    public string Describe()
    {
        if (!IsAvailable || RemainingTime is not { } remaining || WearPercent is not { } wear)
        {
            return UnavailableReason ?? "Zbývající životnost se nepodařilo odhadnout.";
        }

        return string.Format(
            CultureInfo.CurrentCulture,
            "Spotřebováno {0:F0} % životnosti za {1:F0} h provozu. Při stejném způsobu používání "
            + "vydrží disk odhadem ještě {2}. Je to odhad z dosavadního tempa, ne záruka - "
            + "když se způsob používání změní, změní se i výsledek.",
            wear,
            PowerOnHours ?? 0,
            FormatDuration(remaining));
    }

    /// <summary>Roky a měsíce místo tisíců hodin - „61 770 hodin" si nikdo nepředstaví.</summary>
    private static string FormatDuration(TimeSpan remaining)
    {
        var totalDays = remaining.TotalDays;

        if (totalDays >= 365)
        {
            var years = (int)(totalDays / 365);
            var months = (int)((totalDays % 365) / 30);
            return months > 0
                ? $"{years} {YearWord(years)} a {months} {MonthWord(months)}"
                : $"{years} {YearWord(years)}";
        }

        if (totalDays >= 30)
        {
            var months = (int)(totalDays / 30);
            return $"{months} {MonthWord(months)}";
        }

        return $"{Math.Max(1, (int)totalDays)} dní";
    }

    private static string YearWord(int years) => years switch
    {
        1 => "rok",
        >= 2 and <= 4 => "roky",
        _ => "let",
    };

    private static string MonthWord(int months) => months switch
    {
        1 => "měsíc",
        >= 2 and <= 4 => "měsíce",
        _ => "měsíců",
    };
}

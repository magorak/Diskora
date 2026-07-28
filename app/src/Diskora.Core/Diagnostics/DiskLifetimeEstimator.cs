using Diskora.Core.Models;

namespace Diskora.Core.Diagnostics;

/// <summary>
/// Odhadne, kolik životnosti disku ještě zbývá, a hlavně to přeloží do věty,
/// které rozumí i laik. Konkurence (CrystalDiskInfo a spol.) ukáže „Percentage
/// Used: 6 %" a mlčí; tohle je přesně ten údaj, který uživatel opravdu chce znát.
///
/// Počítá se z JEDNOHO čtení, ne z historie: opotřebení za dosavadní dobu provozu
/// dá tempo, tempo dá odhad zbytku. Nepotřebuje tedy čekat týdny na nasbíraná data.
/// Cenou je předpoklad, že se disk bude používat stejně jako dosud - proto se to
/// v textu říká nahlas.
///
/// Zásada: raději mlčet než hádat. Když disk ukazatel opotřebení nemá (běžné
/// u talířových disků), když je opotřebení ještě neměřitelné, nebo když je doba
/// provozu příliš krátká, vrací se „zatím se nedá odhadnout" i s důvodem -
/// vymyšlené číslo je horší než žádné.
/// </summary>
public static class DiskLifetimeEstimator
{
    /// <summary>Pod tímhle opotřebením je tempo tak nepřesné, že by odhad byl fantazie.</summary>
    private const double MinimumWearPercent = 1.0;

    /// <summary>Pod tímhle počtem hodin provozu ještě není z čeho tempo počítat.</summary>
    private const double MinimumPowerOnHours = 100.0;

    /// <summary>Normalizovaný ukazatel opotřebení SSD (100 = nový, 0 = konec životnosti).</summary>
    private const byte MediaWearoutIndicator = 233;

    /// <summary>Doba provozu v hodinách.</summary>
    private const byte PowerOnHoursAttribute = 9;

    public static DiskLifetimeEstimate Estimate(SmartReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var (wearPercent, powerOnHours) = report.NvmeHealth is { } nvme
            ? ((double?)nvme.PercentageUsed, (double?)nvme.PowerOnHours)
            : ReadFromAtaAttributes(report);

        if (wearPercent is null || powerOnHours is null)
        {
            return DiskLifetimeEstimate.Unavailable(
                "Tenhle disk neuvádí, jak je opotřebený. Je to běžné u talířových disků, " +
                "kde se životnost neměří spotřebovanými zápisy - sledujte místo toho přemapované sektory.");
        }

        if (powerOnHours < MinimumPowerOnHours)
        {
            return DiskLifetimeEstimate.Unavailable(
                $"Disk má za sebou teprve {powerOnHours:F0} h provozu. Z tak krátké doby by odhad byl jen dohad - " +
                "zeptejte se znovu později.");
        }

        if (wearPercent < MinimumWearPercent)
        {
            return DiskLifetimeEstimate.Unavailable(
                $"Za {powerOnHours:F0} h provozu se disk zatím měřitelně neopotřeboval (pod 1 %). " +
                "To je dobrá zpráva, ale znamená to, že se tempo nedá spočítat.");
        }

        // Tempo opotřebení za hodinu provozu → kolik hodin zbývá do 100 %.
        var hoursPerPercent = powerOnHours.Value / wearPercent.Value;
        var remainingHours = hoursPerPercent * (100.0 - wearPercent.Value);

        return new DiskLifetimeEstimate(
            true,
            null,
            wearPercent,
            powerOnHours,
            TimeSpan.FromHours(Math.Max(0, remainingHours)));
    }

    /// <summary>
    /// U ATA disků se opotřebení bere z atributu 233, kde normalizovaná hodnota
    /// klesá od 100 k nule - spotřebováno je tedy 100 mínus aktuální hodnota.
    /// </summary>
    private static (double? WearPercent, double? PowerOnHours) ReadFromAtaAttributes(SmartReport report)
    {
        var wearAttribute = report.Attributes.FirstOrDefault(a => a.Id == MediaWearoutIndicator);
        var hoursAttribute = report.Attributes.FirstOrDefault(a => a.Id == PowerOnHoursAttribute);

        if (wearAttribute is null || hoursAttribute is null)
        {
            return (null, null);
        }

        return (100.0 - wearAttribute.CurrentValue, hoursAttribute.RawValue);
    }
}

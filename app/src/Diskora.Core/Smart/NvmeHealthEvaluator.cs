using Diskora.Core.Models;

namespace Diskora.Core.Smart;

/// <summary>
/// Vyhodnocuje riziko z NVMe health logu. NVMe nemá výrobcem dodané prahy pro
/// jednotlivé atributy jako ATA S.M.A.R.T. - specifikace místo toho definuje
/// pár konkrétních polí s jasným významem, takže i pravidla jsou explicitní:
/// 1) Nenulové "critical warning" bity hlásí sám řadič jako kritický stav.
/// 2) Rezervní kapacita na/pod výrobcem daným prahem = kritické (to je jediný
///    práh, který NVMe disk skutečně hlásí).
/// 3) Spotřebovaná životnost a neopravitelné chyby média se hodnotí vlastní
///    heuristikou - viz konstanty níže.
/// </summary>
public static class NvmeHealthEvaluator
{
    /// <summary>Od kolika procent spotřebované životnosti hlásit varování (100 % = konec výrobcem odhadované životnosti).</summary>
    private const byte PercentageUsedWarning = 90;

    /// <summary>
    /// Teplotní práh pro varování. Skutečné prahy disku (WCTEMP/CCTEMP) jsou v
    /// Identify Controller struktuře, ne v health logu - tahle hodnota je proto
    /// konzervativní obecná heuristika, ne údaj z disku.
    /// </summary>
    private const double TemperatureWarningCelsius = 70;

    public static SmartAttributeRisk EvaluateCriticalWarning(byte criticalWarning) =>
        criticalWarning == 0 ? SmartAttributeRisk.Ok : SmartAttributeRisk.Critical;

    public static SmartAttributeRisk EvaluateAvailableSpare(byte sparePercent, byte thresholdPercent) =>
        thresholdPercent > 0 && sparePercent <= thresholdPercent
            ? SmartAttributeRisk.Critical
            : SmartAttributeRisk.Ok;

    public static SmartAttributeRisk EvaluatePercentageUsed(byte percentageUsed) => percentageUsed switch
    {
        >= 100 => SmartAttributeRisk.Critical,
        >= PercentageUsedWarning => SmartAttributeRisk.Warning,
        _ => SmartAttributeRisk.Ok,
    };

    public static SmartAttributeRisk EvaluateMediaErrors(ulong mediaErrors) =>
        mediaErrors > 0 ? SmartAttributeRisk.Warning : SmartAttributeRisk.Ok;

    public static SmartAttributeRisk EvaluateTemperature(double? celsius) =>
        celsius >= TemperatureWarningCelsius ? SmartAttributeRisk.Warning : SmartAttributeRisk.Ok;

    public static DiskHealthStatus EvaluateOverallHealth(NvmeHealthInfo info)
    {
        SmartAttributeRisk[] risks =
        [
            EvaluateCriticalWarning(info.CriticalWarning),
            EvaluateAvailableSpare(info.AvailableSparePercent, info.AvailableSpareThresholdPercent),
            EvaluatePercentageUsed(info.PercentageUsed),
            EvaluateMediaErrors(info.MediaErrors),
            EvaluateTemperature(info.CompositeTemperatureCelsius),
        ];

        if (Array.IndexOf(risks, SmartAttributeRisk.Critical) >= 0)
        {
            return DiskHealthStatus.Critical;
        }

        return Array.IndexOf(risks, SmartAttributeRisk.Warning) >= 0
            ? DiskHealthStatus.Warning
            : DiskHealthStatus.Healthy;
    }
}

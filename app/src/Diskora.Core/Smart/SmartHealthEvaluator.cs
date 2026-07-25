using Diskora.Core.Models;

namespace Diskora.Core.Smart;

/// <summary>
/// Vyhodnocuje riziko z S.M.A.R.T. hodnot. Dvě pravidla:
/// 1) Normalizovaná aktuální hodnota na/pod výrobcem daným prahem = kritické
///    (to je oficiální definice selhání dle ATA SMART specifikace).
/// 2) U vybraných atributů (přemapované/čekající/neopravitelné sektory) je
///    varovné už jakékoli nenulové "raw" číslo, protože práh výrobci bývá
///    nastaven velmi konzervativně a reálný problém se projeví dřív.
/// </summary>
public static class SmartHealthEvaluator
{
    private static readonly HashSet<byte> WarningIfRawNonZero = [5, 196, 197, 198];

    public static SmartAttributeRisk EvaluateAttributeRisk(SmartAttributeReading reading)
    {
        if (reading.Threshold > 0 && reading.CurrentValue <= reading.Threshold)
        {
            return SmartAttributeRisk.Critical;
        }

        if (WarningIfRawNonZero.Contains(reading.Id) && reading.RawValue > 0)
        {
            return SmartAttributeRisk.Warning;
        }

        return SmartAttributeRisk.Ok;
    }

    public static DiskHealthStatus EvaluateOverallHealth(IReadOnlyList<SmartAttributeReading> readings)
    {
        if (readings.Count == 0)
        {
            return DiskHealthStatus.Unknown;
        }

        var risks = readings.Select(EvaluateAttributeRisk).ToList();

        if (risks.Contains(SmartAttributeRisk.Critical))
        {
            return DiskHealthStatus.Critical;
        }

        if (risks.Contains(SmartAttributeRisk.Warning))
        {
            return DiskHealthStatus.Warning;
        }

        return DiskHealthStatus.Healthy;
    }
}

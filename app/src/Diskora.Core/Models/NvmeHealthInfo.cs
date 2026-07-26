namespace Diskora.Core.Models;

/// <summary>
/// Zdravotní údaje NVMe disku (log stránka 0x02), přepočtené do uživatelských
/// jednotek. Na rozdíl od ATA S.M.A.R.T. nejde o seznam atributů s
/// normalizovanou/nejhorší/prahovou hodnotou - NVMe specifikace definuje pevnou
/// sadu pojmenovaných polí, takže se model nesnaží tvářit jako ATA atributy.
/// </summary>
public sealed record NvmeHealthInfo(
    byte CriticalWarning,
    ushort CompositeTemperatureKelvin,
    byte AvailableSparePercent,
    byte AvailableSpareThresholdPercent,
    byte PercentageUsed,
    ulong DataUnitsRead,
    ulong DataUnitsWritten,
    ulong PowerCycles,
    ulong PowerOnHours,
    ulong UnsafeShutdowns,
    ulong MediaErrors,
    ulong ErrorLogEntryCount)
{
    /// <summary>Jedna "datová jednotka" NVMe je dle specifikace 1000 × 512 bajtů.</summary>
    public const ulong BytesPerDataUnit = 512UL * 1000UL;

    /// <summary>Teplota ve stupních Celsia, nebo null když ji řadič nehlásí (pole je 0).</summary>
    public double? CompositeTemperatureCelsius =>
        CompositeTemperatureKelvin == 0 ? null : CompositeTemperatureKelvin - 273.15;

    public ulong BytesRead => DataUnitsRead * BytesPerDataUnit;

    public ulong BytesWritten => DataUnitsWritten * BytesPerDataUnit;
}

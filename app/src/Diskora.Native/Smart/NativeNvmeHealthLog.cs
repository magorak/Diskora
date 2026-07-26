namespace Diskora.Native.Smart;

/// <summary>
/// Syrový obsah NVMe logu "SMART / Health Information" (log page 0x02), tak jak
/// ho vrátil řadič - bez jakéhokoli přepočtu na uživatelské jednotky. Převod
/// (Kelvin → °C, datové jednotky → bajty) a hodnocení rizika dělá až vrstva
/// Diskora.Core, stejně jako u ATA atributů.
/// </summary>
/// <param name="CriticalWarning">
/// Bitové pole varování dle NVMe specifikace: bit 0 = došla rezervní kapacita,
/// bit 1 = teplota mimo povolený rozsah, bit 2 = zhoršená spolehlivost média,
/// bit 3 = médium přepnuto do režimu jen pro čtení, bit 4 = selhala záloha
/// volatilní paměti, bit 5 = perzistentní paměťová oblast jen pro čtení.
/// </param>
/// <param name="CompositeTemperatureKelvin">Souhrnná teplota řadiče v Kelvinech (0 = nehlášeno).</param>
/// <param name="AvailableSparePercent">Zbývající rezervní kapacita v procentech.</param>
/// <param name="AvailableSpareThresholdPercent">Práh, pod kterým výrobce hlásí kritický nedostatek rezervy.</param>
/// <param name="PercentageUsed">Spotřebovaná životnost v procentech (100 = vyčerpaná výrobcem odhadovaná životnost).</param>
/// <param name="DataUnitsRead">Přečteno v jednotkách po 1000 × 512 B.</param>
/// <param name="DataUnitsWritten">Zapsáno v jednotkách po 1000 × 512 B.</param>
/// <param name="PowerCycles">Počet zapnutí.</param>
/// <param name="PowerOnHours">Doba provozu v hodinách.</param>
/// <param name="UnsafeShutdowns">Počet nekorektních vypnutí (ztráta napájení bez řádného odhlášení).</param>
/// <param name="MediaErrors">Počet neopravitelných chyb integrity dat.</param>
/// <param name="ErrorLogEntryCount">Počet záznamů v chybovém logu řadiče za celou dobu životnosti.</param>
public sealed record NativeNvmeHealthLog(
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
    ulong ErrorLogEntryCount);

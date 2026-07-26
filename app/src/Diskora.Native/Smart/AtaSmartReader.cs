namespace Diskora.Native.Smart;

/// <summary>
/// Vstupní bod pro čtení S.M.A.R.T. dat z ATA/SATA disků. Sám žádné IOCTL
/// neposílá - vybírá mezi dvěma cestami, protože žádná z nich nefunguje všude:
/// <list type="number">
/// <item>ATA pass-through (<see cref="AtaPassThroughSmartReader"/>) - obecný kanál
/// pro libovolný ATA příkaz, podporovaný širší škálou řadičů, proto se zkouší první.</item>
/// <item>Legacy IOCTL_SMART_RCV_DRIVE_DATA (<see cref="LegacySmartIoctlReader"/>) -
/// starší, ale u některých ovladačů jediná funkční cesta, proto zůstává jako záloha.</item>
/// </list>
/// Přes USB mosty, hardwarové RAID řadiče a u NVMe disků typicky selžou obě - to je
/// očekávané omezení ATA API, ne chyba; volající to musí zobrazit jako "SMART
/// nedostupné", ne pád. Pro NVMe disky existuje samostatná cesta, viz
/// <see cref="NvmeHealthReader"/>. Vyžaduje práva administrátora.
/// </summary>
public static class AtaSmartReader
{
    public static NativeSmartReadResult Read(int physicalDriveIndex)
    {
        var passThrough = AtaPassThroughSmartReader.Read(physicalDriveIndex);
        if (IsUsable(passThrough))
        {
            return passThrough;
        }

        var legacy = LegacySmartIoctlReader.Read(physicalDriveIndex);
        if (IsUsable(legacy))
        {
            return legacy;
        }

        return new NativeSmartReadResult(false, CombineFailureReasons(passThrough, legacy), []);
    }

    /// <summary>
    /// Úspěch s prázdnou tabulkou se bere jako neúspěch: disk se zapnutým SMART
    /// vždy hlásí aspoň několik atributů, takže prázdný výsledek znamená, že
    /// příkaz sice formálně prošel, ale data nedorazila - a druhá cesta má šanci
    /// uspět. Prázdný seznam nahlášený jako "v pořádku" by uživateli tvrdil,
    /// že disk je bez problémů, aniž by cokoli změřil.
    /// </summary>
    private static bool IsUsable(NativeSmartReadResult result) => result.Success && result.Attributes.Count > 0;

    private static string CombineFailureReasons(NativeSmartReadResult passThrough, NativeSmartReadResult legacy)
    {
        var passThroughReason = passThrough.FailureReason;
        var legacyReason = legacy.FailureReason;

        if (string.IsNullOrEmpty(passThroughReason))
        {
            return legacyReason ?? "S.M.A.R.T. data se nepodařilo přečíst.";
        }

        // Když obě cesty selhaly ze stejného důvodu (typicky nešel otevřít disk
        // bez elevace), nemá smysl uživateli tu samou větu opakovat dvakrát.
        if (string.IsNullOrEmpty(legacyReason) || string.Equals(passThroughReason, legacyReason, StringComparison.Ordinal))
        {
            return passThroughReason;
        }

        return $"{passThroughReason} Nepomohla ani starší cesta: {legacyReason}";
    }
}

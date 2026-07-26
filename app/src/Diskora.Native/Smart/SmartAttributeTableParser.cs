namespace Diskora.Native.Smart;

/// <summary>
/// Rozkládá 512bajtový blok, který ATA disk vrací na příkazy SMART READ DATA
/// (0xD0) a SMART READ THRESHOLDS (0xD1). Formát bloku je stejný bez ohledu na
/// to, jakým IOCTL se k němu došlo - sdílí ho proto legacy cesta
/// (<see cref="LegacySmartIoctlReader"/>) i ATA pass-through
/// (<see cref="AtaPassThroughSmartReader"/>).
/// </summary>
public static class SmartAttributeTableParser
{
    /// <summary>Prvních 512 bajtů odpovědi; první 2 bajty jsou revize struktury, pak následují záznamy.</summary>
    public const int DataSize = 512;

    private const int TableOffset = 2;
    private const int EntrySize = 12;
    private const int EntryCount = 30;

    /// <summary>
    /// Mapa ID atributu → práh selhání z bloku SMART READ THRESHOLDS.
    /// Záznamy s ID 0 jsou dle specifikace nepoužité a přeskakují se.
    /// </summary>
    public static Dictionary<byte, byte> ParseThresholds(ReadOnlySpan<byte> data)
    {
        var thresholds = new Dictionary<byte, byte>();

        for (var i = 0; i < EntryCount; i++)
        {
            var offset = TableOffset + (i * EntrySize);
            if (offset + EntrySize > data.Length)
            {
                break;
            }

            var id = data[offset];
            if (id != 0)
            {
                thresholds[id] = data[offset + 1];
            }
        }

        return thresholds;
    }

    /// <summary>
    /// Atributy z bloku SMART READ DATA. Prahy jsou nepovinné - disky, které
    /// příkaz 0xD1 (ve novějších revizích ATA označený jako zastaralý) odmítnou,
    /// tak stále dají použitelné hodnoty, jen bez prahu selhání.
    /// </summary>
    public static List<NativeSmartAttribute> ParseAttributes(
        ReadOnlySpan<byte> data,
        IReadOnlyDictionary<byte, byte>? thresholds = null)
    {
        var attributes = new List<NativeSmartAttribute>();

        for (var i = 0; i < EntryCount; i++)
        {
            var offset = TableOffset + (i * EntrySize);
            if (offset + EntrySize > data.Length)
            {
                break;
            }

            var id = data[offset];
            if (id == 0)
            {
                continue;
            }

            // Syrová hodnota je 48bitová (6 bajtů, little-endian) - proto se
            // neskládá přes BitConverter, který nemá 48bitovou variantu.
            ulong raw = 0;
            for (var b = 0; b < 6; b++)
            {
                raw |= (ulong)data[offset + 5 + b] << (b * 8);
            }

            byte threshold = 0;
            thresholds?.TryGetValue(id, out threshold);

            attributes.Add(new NativeSmartAttribute(id, data[offset + 3], data[offset + 4], threshold, raw));
        }

        return attributes;
    }
}

namespace Diskora.VirtualDisks;

/// <summary>
/// Needestruktivní čtení rozvržení oblastí (MBR/GPT) z raw/dd obrazu disku -
/// obyčejné čtení souboru, žádné mountování ani admin práva (na rozdíl od
/// VHD/VHDX - viz <see cref="VirtualDiskAttacher"/>). Windows nemá pro raw
/// obrazy žádný formátovaný kontejner ani podporu v Mount-DiskImage (živě
/// ověřeno: "soubor je porušen a není čitelný"), takže tohle je jediná
/// rozumná bezobslužná inspekce bez závislosti na třetí straně.
///
/// Rozpoznává jen primární MBR záznamy (rozšířené/logické oddíly přes EBR
/// řetěz se neprochází) a standardní GPT hlavičku - pro účel "kolik oblastí
/// a jakého schématu" to stačí, detailní typ/název jednotlivých oddílů
/// Diskora v této fázi neřeší.
/// </summary>
public static class RawImageInspector
{
    private const int SectorSize = 512;
    private const int PrefixReadSize = 1024 * 1024; // s rezervou pokryje MBR i celou GPT tabulku
    private const int MaxPartitionEntriesRead = 512; // obranná mez proti nesmyslné/poškozené hlavičce

    public static RawImageInspectionResult Inspect(string path)
    {
        byte[] prefix;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var length = (int)Math.Min(PrefixReadSize, stream.Length);
            prefix = new byte[length];
            var read = 0;
            while (read < length)
            {
                var n = stream.Read(prefix, read, length - read);
                if (n == 0)
                {
                    break;
                }

                read += n;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new RawImageInspectionResult(false, $"Soubor se nepodařilo přečíst ({ex.Message}).", RawImagePartitionScheme.Unknown, 0);
        }

        if (prefix.Length < SectorSize || prefix[510] != 0x55 || prefix[511] != 0xAA)
        {
            return new RawImageInspectionResult(true, null, RawImagePartitionScheme.Unknown, 0);
        }

        var firstEntryType = prefix[446 + 4];
        if (firstEntryType == 0xEE && prefix.Length >= 2 * SectorSize)
        {
            return InspectGpt(prefix);
        }

        var mbrCount = 0;
        for (var i = 0; i < 4; i++)
        {
            var entryOffset = 446 + (i * 16);
            if (prefix[entryOffset + 4] != 0x00)
            {
                mbrCount++;
            }
        }

        return new RawImageInspectionResult(true, null, RawImagePartitionScheme.Mbr, mbrCount);
    }

    private static RawImageInspectionResult InspectGpt(byte[] prefix)
    {
        var header = prefix.AsSpan(SectorSize, SectorSize);

        if (!header[..8].SequenceEqual("EFI PART"u8))
        {
            // Protective MBR bez čitelné GPT hlavičky - vzácné, ale ne důvod k pádu.
            return new RawImageInspectionResult(true, null, RawImagePartitionScheme.Unknown, 0);
        }

        var entriesStartLba = BitConverter.ToInt64(header[72..80]);
        var entryCount = Math.Min(BitConverter.ToInt32(header[80..84]), MaxPartitionEntriesRead);
        var entrySize = BitConverter.ToInt32(header[84..88]);

        var tableStart = (int)(entriesStartLba * SectorSize);
        var tableEnd = tableStart + (entryCount * entrySize);

        if (entrySize <= 0 || tableStart < 0 || tableEnd > prefix.Length)
        {
            // Tabulka oddílů je mimo přečtenou předponu souboru - vrátí se
            // aspoň rozpoznané schéma, bez spolehlivého počtu oddílů.
            return new RawImageInspectionResult(true, null, RawImagePartitionScheme.Gpt, 0);
        }

        var usedCount = 0;
        for (var i = 0; i < entryCount; i++)
        {
            var entry = prefix.AsSpan(tableStart + (i * entrySize), 16); // prvních 16 B = typové GUID
            if (entry.IndexOfAnyExcept((byte)0) >= 0)
            {
                usedCount++;
            }
        }

        return new RawImageInspectionResult(true, null, RawImagePartitionScheme.Gpt, usedCount);
    }
}

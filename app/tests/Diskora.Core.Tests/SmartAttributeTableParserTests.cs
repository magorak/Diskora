using Diskora.Native.Smart;

namespace Diskora.Core.Tests;

public class SmartAttributeTableParserTests
{
    /// <summary>
    /// Sestaví blok v tom tvaru, v jakém ho vrací disk: 2 bajty revize, pak
    /// záznamy po 12 bajtech (ID, flagy(2), aktuální, nejhorší, 6 bajtů syrové
    /// hodnoty little-endian, rezerva).
    /// </summary>
    private static byte[] BuildAttributeBlock(params (byte Id, byte Current, byte Worst, ulong Raw)[] entries)
    {
        var data = new byte[SmartAttributeTableParser.DataSize];
        data[0] = 0x10; // revize struktury

        for (var i = 0; i < entries.Length; i++)
        {
            var offset = 2 + (i * 12);
            var (id, current, worst, raw) = entries[i];

            data[offset] = id;
            data[offset + 3] = current;
            data[offset + 4] = worst;
            for (var b = 0; b < 6; b++)
            {
                data[offset + 5 + b] = (byte)(raw >> (b * 8));
            }
        }

        return data;
    }

    private static byte[] BuildThresholdBlock(params (byte Id, byte Threshold)[] entries)
    {
        var data = new byte[SmartAttributeTableParser.DataSize];

        for (var i = 0; i < entries.Length; i++)
        {
            var offset = 2 + (i * 12);
            data[offset] = entries[i].Id;
            data[offset + 1] = entries[i].Threshold;
        }

        return data;
    }

    [Fact]
    public void ParseAttributes_PrectePolePodleSpecifikace()
    {
        var data = BuildAttributeBlock((5, 100, 98, 3), (194, 70, 55, 30));

        var attributes = SmartAttributeTableParser.ParseAttributes(data);

        Assert.Equal(2, attributes.Count);
        Assert.Equal(5, attributes[0].Id);
        Assert.Equal(100, attributes[0].CurrentValue);
        Assert.Equal(98, attributes[0].WorstValue);
        Assert.Equal(3UL, attributes[0].RawValue);
        Assert.Equal(194, attributes[1].Id);
        Assert.Equal(30UL, attributes[1].RawValue);
    }

    [Fact]
    public void ParseAttributes_SyrovaHodnotaJe48bitova()
    {
        // Doba provozu i "celkem zapsáno" u SSD běžně přerostou 32 bitů;
        // ořez na 4 bajty by z nich udělal nesmyslně malá čísla.
        const ulong raw = 0xFFEEDDCCBBAA;
        var data = BuildAttributeBlock((9, 100, 100, raw));

        var attributes = SmartAttributeTableParser.ParseAttributes(data);

        Assert.Equal(raw, attributes[0].RawValue);
    }

    [Fact]
    public void ParseAttributes_PreskociNepouziteZaznamy()
    {
        var data = BuildAttributeBlock((0, 100, 100, 0), (5, 90, 90, 1));

        var attributes = SmartAttributeTableParser.ParseAttributes(data);

        Assert.Single(attributes);
        Assert.Equal(5, attributes[0].Id);
    }

    [Fact]
    public void ParseAttributes_BezPrahuJeVsudeNula()
    {
        // Disky odmítající zastaralý příkaz 0xD1 musí i tak dát použitelné
        // atributy - práh 0 pak SmartHealthEvaluator vyhodnotí jako "bez prahu".
        var data = BuildAttributeBlock((5, 100, 100, 0));

        var attributes = SmartAttributeTableParser.ParseAttributes(data, thresholds: null);

        Assert.Equal(0, attributes[0].Threshold);
    }

    [Fact]
    public void ParseAttributes_SparujePrahyPodleId()
    {
        var data = BuildAttributeBlock((5, 100, 100, 0), (194, 70, 70, 0));
        var thresholds = SmartAttributeTableParser.ParseThresholds(BuildThresholdBlock((194, 45), (5, 10)));

        var attributes = SmartAttributeTableParser.ParseAttributes(data, thresholds);

        Assert.Equal(10, attributes.Single(a => a.Id == 5).Threshold);
        Assert.Equal(45, attributes.Single(a => a.Id == 194).Threshold);
    }

    [Fact]
    public void ParseAttributes_NeprecteVicNezTricetZaznamu()
    {
        // Tabulka má dle specifikace pevných 30 míst; 2 + 30 × 12 = 362 bajtů,
        // zbytek bloku patří jiným polím a nesmí se číst jako atributy.
        var data = new byte[SmartAttributeTableParser.DataSize];
        for (var offset = 2; offset + 12 <= data.Length; offset += 12)
        {
            data[offset] = 1;
        }

        Assert.Equal(30, SmartAttributeTableParser.ParseAttributes(data).Count);
    }

    [Fact]
    public void ParseAttributes_ZkracenyBlokNespadne()
    {
        var attributes = SmartAttributeTableParser.ParseAttributes(new byte[10]);

        Assert.Empty(attributes);
    }

    [Fact]
    public void ParseThresholds_PreskociNepouziteZaznamy()
    {
        var thresholds = SmartAttributeTableParser.ParseThresholds(BuildThresholdBlock((0, 99), (5, 10)));

        Assert.Single(thresholds);
        Assert.Equal(10, thresholds[5]);
    }
}

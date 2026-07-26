using Diskora.Core.Models;
using Diskora.Core.Smart;

namespace Diskora.Core.Tests;

public class NvmeHealthCatalogTests
{
    private static NvmeHealthInfo Sample(byte criticalWarning = 0) => new(
        CriticalWarning: criticalWarning,
        CompositeTemperatureKelvin: 315,
        AvailableSparePercent: 100,
        AvailableSpareThresholdPercent: 10,
        PercentageUsed: 6,
        DataUnitsRead: 134330668,
        DataUnitsWritten: 77636391,
        PowerCycles: 1562,
        PowerOnHours: 3943,
        UnsafeShutdowns: 44,
        MediaErrors: 0,
        ErrorLogEntryCount: 0);

    [Fact]
    public void Describe_KazdyRadekMaNazevHodnotuIVysvetleni()
    {
        var metrics = NvmeHealthCatalog.Describe(Sample());

        Assert.NotEmpty(metrics);
        Assert.All(metrics, metric =>
        {
            Assert.False(string.IsNullOrWhiteSpace(metric.Name));
            Assert.False(string.IsNullOrWhiteSpace(metric.Value));
            Assert.False(string.IsNullOrWhiteSpace(metric.Explanation));
        });
    }

    [Fact]
    public void DescribeCriticalWarning_ZadneVarovani()
    {
        Assert.Equal("žádné", NvmeHealthCatalog.DescribeCriticalWarning(0));
    }

    [Fact]
    public void DescribeCriticalWarning_VypiseVsechnyRozsvicenBity()
    {
        var description = NvmeHealthCatalog.DescribeCriticalWarning(0x01 | 0x08);

        Assert.Contains("došla rezervní kapacita", description, StringComparison.Ordinal);
        Assert.Contains("jen pro čtení", description, StringComparison.Ordinal);
    }

    [Fact]
    public void DescribeCriticalWarning_NeznamyBitSeNeztrati()
    {
        // Rezervovaný bit dnes bez významu nesmí skončit jako "žádné" - disk něco hlásí.
        var description = NvmeHealthCatalog.DescribeCriticalWarning(0x40);

        Assert.DoesNotContain("žádné", description, StringComparison.Ordinal);
        Assert.Contains("0x40", description, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_KritickeVarovaniSePropiseDoRizikaRadku()
    {
        var metrics = NvmeHealthCatalog.Describe(Sample(criticalWarning: 0x02));

        var warningRow = metrics.First(m => m.Name == "Varování řadiče");
        Assert.Equal(SmartAttributeRisk.Critical, warningRow.Risk);
        Assert.Contains("teplota", warningRow.Value, StringComparison.Ordinal);
    }
}

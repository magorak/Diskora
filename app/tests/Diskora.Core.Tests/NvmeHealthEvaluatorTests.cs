using Diskora.Core.Models;
using Diskora.Core.Smart;

namespace Diskora.Core.Tests;

public class NvmeHealthEvaluatorTests
{
    /// <summary>Zdravý disk odpovídající reálným hodnotám čtenému NVMe disku v testovacím prostředí.</summary>
    private static NvmeHealthInfo Healthy() => new(
        CriticalWarning: 0,
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
    public void EvaluateOverallHealth_ZdravyDisk_JeHealthy()
    {
        Assert.Equal(DiskHealthStatus.Healthy, NvmeHealthEvaluator.EvaluateOverallHealth(Healthy()));
    }

    [Fact]
    public void EvaluateOverallHealth_NenulovaVarovaniRadice_JeCritical()
    {
        var info = Healthy() with { CriticalWarning = 0x04 };

        Assert.Equal(DiskHealthStatus.Critical, NvmeHealthEvaluator.EvaluateOverallHealth(info));
    }

    [Fact]
    public void EvaluateAvailableSpare_NaPrahu_JeCritical()
    {
        // Specifikace mluví o "na nebo pod prahem", ne jen "pod".
        Assert.Equal(SmartAttributeRisk.Critical, NvmeHealthEvaluator.EvaluateAvailableSpare(10, 10));
        Assert.Equal(SmartAttributeRisk.Critical, NvmeHealthEvaluator.EvaluateAvailableSpare(9, 10));
        Assert.Equal(SmartAttributeRisk.Ok, NvmeHealthEvaluator.EvaluateAvailableSpare(11, 10));
    }

    [Fact]
    public void EvaluateAvailableSpare_NulovyPrah_NehlasiKriticky()
    {
        // Disk, který práh nehlásí (0), by jinak vyšel jako kritický při jakékoli
        // hodnotě rezervy - včetně 0 %, což u zdravého disku bez podpory nastává.
        Assert.Equal(SmartAttributeRisk.Ok, NvmeHealthEvaluator.EvaluateAvailableSpare(0, 0));
    }

    [Theory]
    [InlineData((byte)0, SmartAttributeRisk.Ok)]
    [InlineData((byte)89, SmartAttributeRisk.Ok)]
    [InlineData((byte)90, SmartAttributeRisk.Warning)]
    [InlineData((byte)99, SmartAttributeRisk.Warning)]
    [InlineData((byte)100, SmartAttributeRisk.Critical)]
    [InlineData((byte)255, SmartAttributeRisk.Critical)]
    public void EvaluatePercentageUsed_StupnujeSeSOpotrebenim(byte percentageUsed, SmartAttributeRisk expected)
    {
        Assert.Equal(expected, NvmeHealthEvaluator.EvaluatePercentageUsed(percentageUsed));
    }

    [Fact]
    public void EvaluateMediaErrors_JakakoliNenulovaHodnota_JeVarovani()
    {
        Assert.Equal(SmartAttributeRisk.Ok, NvmeHealthEvaluator.EvaluateMediaErrors(0));
        Assert.Equal(SmartAttributeRisk.Warning, NvmeHealthEvaluator.EvaluateMediaErrors(1));
    }

    [Fact]
    public void EvaluateTemperature_NehlasenaTeplota_NeniVarovani()
    {
        // Řadič, který teplotu nehlásí, posílá 0 K - to se nesmí zaměnit za "0 °C".
        Assert.Null(Healthy() with { CompositeTemperatureKelvin = 0 } is var info ? info.CompositeTemperatureCelsius : null);
        Assert.Equal(SmartAttributeRisk.Ok, NvmeHealthEvaluator.EvaluateTemperature(null));
    }

    [Fact]
    public void EvaluateTemperature_NadPrahem_JeVarovani()
    {
        Assert.Equal(SmartAttributeRisk.Ok, NvmeHealthEvaluator.EvaluateTemperature(69));
        Assert.Equal(SmartAttributeRisk.Warning, NvmeHealthEvaluator.EvaluateTemperature(70));
    }

    [Fact]
    public void EvaluateOverallHealth_VarovaniNeprebijiKritickyStav()
    {
        var info = Healthy() with { MediaErrors = 5, AvailableSparePercent = 5 };

        Assert.Equal(DiskHealthStatus.Critical, NvmeHealthEvaluator.EvaluateOverallHealth(info));
    }

    [Fact]
    public void NvmeHealthInfo_PrepocitavaJednotkyDleSpecifikace()
    {
        var info = Healthy() with { DataUnitsWritten = 1, DataUnitsRead = 2 };

        // Jedna datová jednotka = 1000 × 512 B.
        Assert.Equal(512_000UL, info.BytesWritten);
        Assert.Equal(1_024_000UL, info.BytesRead);
        Assert.Equal(41.85, info.CompositeTemperatureCelsius!.Value, 2);
    }
}

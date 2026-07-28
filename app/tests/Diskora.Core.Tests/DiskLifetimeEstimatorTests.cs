using Diskora.Core.Diagnostics;
using Diskora.Core.Models;

namespace Diskora.Core.Tests;

public class DiskLifetimeEstimatorTests
{
    private static SmartReport Nvme(byte percentageUsed, ulong powerOnHours) => new(
        0, DateTimeOffset.UtcNow, [], DiskHealthStatus.Healthy,
        new NvmeHealthInfo(0, 315, 100, 10, percentageUsed, 0, 0, 0, powerOnHours, 0, 0, 0));

    private static SmartReport Ata(params SmartAttributeReading[] attributes) =>
        new(0, DateTimeOffset.UtcNow, attributes, DiskHealthStatus.Healthy);

    [Fact]
    public void Estimate_Nvme_SpocitaZbytekZTempaOpotrebeni()
    {
        // 10 % za 1000 h → 100 h na procento → zbývá 90 % = 9000 h.
        var estimate = DiskLifetimeEstimator.Estimate(Nvme(percentageUsed: 10, powerOnHours: 1000));

        Assert.True(estimate.IsAvailable);
        Assert.Equal(10, estimate.WearPercent);
        Assert.Equal(9000, estimate.RemainingTime!.Value.TotalHours, 1);
    }

    [Fact]
    public void Estimate_Nvme_ReaklnyDiskZTestovacihoStroje()
    {
        // Samsung 980 PRO: 6 % za 3943 h → zhruba 61 800 h, tedy přes 7 let.
        var estimate = DiskLifetimeEstimator.Estimate(Nvme(percentageUsed: 6, powerOnHours: 3943));

        Assert.True(estimate.IsAvailable);
        Assert.InRange(estimate.RemainingTime!.Value.TotalHours, 61_000, 62_500);
        Assert.Contains("let", estimate.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void Estimate_TemerVycerpanyDisk_HlasiKratkyZbytek()
    {
        var estimate = DiskLifetimeEstimator.Estimate(Nvme(percentageUsed: 99, powerOnHours: 20_000));

        Assert.True(estimate.IsAvailable);
        Assert.InRange(estimate.RemainingTime!.Value.TotalHours, 180, 220);
    }

    [Fact]
    public void Estimate_VycerpanyDisk_NevraciZapornyCas()
    {
        var estimate = DiskLifetimeEstimator.Estimate(Nvme(percentageUsed: 100, powerOnHours: 20_000));

        Assert.True(estimate.RemainingTime!.Value >= TimeSpan.Zero);
    }

    [Fact]
    public void Estimate_ZatimNemeritelneOpotrebeni_RadsiMlci()
    {
        // Nulové opotřebení nejde extrapolovat - dělení nulou by dalo nekonečno.
        var estimate = DiskLifetimeEstimator.Estimate(Nvme(percentageUsed: 0, powerOnHours: 5000));

        Assert.False(estimate.IsAvailable);
        Assert.Contains("neopotřeboval", estimate.UnavailableReason!, StringComparison.Ordinal);
    }

    [Fact]
    public void Estimate_PrilisKratkaDobaProvozu_RadsiMlci()
    {
        var estimate = DiskLifetimeEstimator.Estimate(Nvme(percentageUsed: 5, powerOnHours: 20));

        Assert.False(estimate.IsAvailable);
        Assert.Contains("krátké doby", estimate.UnavailableReason!, StringComparison.Ordinal);
    }

    [Fact]
    public void Estimate_DiskBezUkazateleOpotrebeni_VysvetliProc()
    {
        // Typický talířový disk: má dobu provozu, ale ukazatel opotřebení ne.
        var estimate = DiskLifetimeEstimator.Estimate(Ata(new SmartAttributeReading(9, 100, 100, 0, 20_000)));

        Assert.False(estimate.IsAvailable);
        Assert.Contains("talířových disků", estimate.UnavailableReason!, StringComparison.Ordinal);
    }

    [Fact]
    public void Estimate_AtaSsd_PocitaOpotrebeniZNormalizovaneHodnoty()
    {
        // Atribut 233 klesá od 100 dolů, takže spotřebováno je 100 - aktuální.
        var estimate = DiskLifetimeEstimator.Estimate(Ata(
            new SmartAttributeReading(233, 80, 80, 0, 0),
            new SmartAttributeReading(9, 100, 100, 0, 4000)));

        Assert.True(estimate.IsAvailable);
        Assert.Equal(20, estimate.WearPercent);
        Assert.Equal(16_000, estimate.RemainingTime!.Value.TotalHours, 1);
    }

    [Fact]
    public void Describe_UvadiZeJdeOOdhad_NeZaruku()
    {
        var text = DiskLifetimeEstimator.Estimate(Nvme(10, 1000)).Describe();

        Assert.Contains("odhad", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ne záruka", text, StringComparison.Ordinal);
    }

    // Při opotřebení 50 % se zbývající čas rovná dosavadní době provozu,
    // takže hodiny níže jsou přímo výsledný odhad: 1 rok / 3 roky / 7 let.
    [Theory]
    [InlineData(50, 8_760, "rok")]
    [InlineData(50, 26_280, "roky")]
    [InlineData(50, 61_320, "let")]
    public void Describe_SklonujeRokySpravne(byte wear, ulong hours, string expectedWord)
    {
        var text = DiskLifetimeEstimator.Estimate(Nvme(wear, hours)).Describe();

        Assert.Contains(expectedWord, text, StringComparison.Ordinal);
    }
}

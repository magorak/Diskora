using Diskora.Core.Diagnostics;
using Diskora.Core.Models;

namespace Diskora.Core.Tests;

public class DiskDoctorAdvisorTests
{
    private static SmartReadResult HealthySmart(params SmartAttributeReading[] attributes) =>
        new(true, null, new SmartReport(0, DateTimeOffset.UtcNow, attributes, DiskHealthStatus.Healthy));

    private static DiskDoctorInputs Inputs(
        SmartReadResult? smart = null,
        VolumeDirtyState dirty = VolumeDirtyState.Clean,
        bool? hasSeekPenalty = null,
        bool? supportsTrim = null,
        bool isAdmin = true) =>
        new(smart ?? HealthySmart(),
            dirty,
            new DiskOptimizationCapabilities(hasSeekPenalty, supportsTrim),
            isAdmin);

    private static DiskDoctorReport Run(DiskDoctorInputs inputs) => DiskDoctorAdvisor.Diagnose("Testovací disk", inputs);

    [Fact]
    public void Diagnose_ZdravyDiskCistySvazek_JeCelkoveOk()
    {
        var report = Run(Inputs());

        Assert.Equal(DiskDoctorSeverity.Ok, report.Overall);
        Assert.Equal("Testovací disk", report.Subject);
        Assert.All(report.Findings, f => Assert.Equal(DiskDoctorSeverity.Ok, f.Severity));
    }

    [Fact]
    public void Diagnose_CelkovyVerdiktJeNejhorsiZjisteni()
    {
        var critical = new SmartReadResult(true, null,
            new SmartReport(0, DateTimeOffset.UtcNow, [], DiskHealthStatus.Critical));

        var report = Run(Inputs(smart: critical, dirty: VolumeDirtyState.Dirty));

        Assert.Equal(DiskDoctorSeverity.Critical, report.Overall);
    }

    [Fact]
    public void Diagnose_KritickeZdravi_DoporuciZalohu()
    {
        var critical = new SmartReadResult(true, null,
            new SmartReport(0, DateTimeOffset.UtcNow, [], DiskHealthStatus.Critical));

        var report = Run(Inputs(smart: critical));

        Assert.Contains(report.Findings, f => f.RecommendedAction == DiskDoctorAction.BackUpNow);
    }

    [Fact]
    public void Diagnose_SmartNedostupneBezElevace_DoporuciSpustitJakoSpravce()
    {
        var unavailable = new SmartReadResult(false, "Win32 chyba 5", null);

        var report = Run(Inputs(smart: unavailable, isAdmin: false));

        var finding = report.Findings.Single(f => f.Title.Contains("Zdraví disku", StringComparison.Ordinal));
        Assert.Equal(DiskDoctorAction.RunAsAdministrator, finding.RecommendedAction);
    }

    [Fact]
    public void Diagnose_SmartNedostupneSElevaci_NenavrhujeElevaci()
    {
        // S admin právy už elevace nepomůže - disk prostě data nedává (USB most, RAID).
        var unavailable = new SmartReadResult(false, "Win32 chyba 1", null);

        var report = Run(Inputs(smart: unavailable, isAdmin: true));

        Assert.DoesNotContain(report.Findings, f => f.RecommendedAction == DiskDoctorAction.RunAsAdministrator);
        Assert.Equal(DiskDoctorSeverity.Info, report.Overall);
    }

    [Fact]
    public void Diagnose_VadneSektory_DoporuciPovrchovySken()
    {
        // ID 5 (přemapované sektory) s nenulovou surovou hodnotou = varování.
        var smart = new SmartReadResult(true, null, new SmartReport(
            0, DateTimeOffset.UtcNow,
            [new SmartAttributeReading(5, 100, 100, 0, RawValue: 12)],
            DiskHealthStatus.Warning));

        var report = Run(Inputs(smart: smart));

        var finding = report.Findings.Single(f => f.RecommendedAction == DiskDoctorAction.RunSurfaceScan);
        Assert.Contains("12", finding.Title, StringComparison.Ordinal);
    }

    [Fact]
    public void Diagnose_ChybyPrenosuPoKabelu_HlasiSeIKdyzJeDiskZdravy()
    {
        // Atribut 199 nezhoršuje normalizovanou hodnotu, takže disk vyjde jako
        // zdravý - přesto to uživatel má vědět, protože jde o vadný kabel.
        var smart = HealthySmart(new SmartAttributeReading(199, 200, 200, 0, RawValue: 2426));

        var report = Run(Inputs(smart: smart));

        var finding = report.Findings.Single(f => f.RecommendedAction == DiskDoctorAction.CheckCable);
        Assert.Equal(DiskDoctorSeverity.Info, finding.Severity);
        Assert.Contains("2426", finding.Title, StringComparison.Ordinal);
    }

    [Fact]
    public void Diagnose_NulovyPocetChybKabelu_Nehlasi()
    {
        var report = Run(Inputs(smart: HealthySmart(new SmartAttributeReading(199, 200, 200, 0, RawValue: 0))));

        Assert.DoesNotContain(report.Findings, f => f.RecommendedAction == DiskDoctorAction.CheckCable);
    }

    [Fact]
    public void Diagnose_NvmeVycerpanaRezerva_JeKritickeADoporuciZalohu()
    {
        var nvme = new NvmeHealthInfo(0, 315, AvailableSparePercent: 5, AvailableSpareThresholdPercent: 10,
            PercentageUsed: 20, 0, 0, 0, 0, 0, MediaErrors: 0, ErrorLogEntryCount: 0);
        var smart = new SmartReadResult(true, null,
            new SmartReport(0, DateTimeOffset.UtcNow, [], DiskHealthStatus.Critical, nvme));

        var report = Run(Inputs(smart: smart));

        Assert.Equal(DiskDoctorSeverity.Critical, report.Overall);
        Assert.Contains(report.Findings, f => f.Title.Contains("rezervní kapacita", StringComparison.Ordinal));
    }

    [Fact]
    public void Diagnose_PoskozenySouborovySystem_DoporuciOpravu()
    {
        var report = Run(Inputs(dirty: VolumeDirtyState.Dirty));

        var finding = report.Findings.Single(f => f.RecommendedAction == DiskDoctorAction.RunSpotFix);
        Assert.Equal(DiskDoctorSeverity.Warning, finding.Severity);
    }

    [Fact]
    public void Diagnose_NeznamyStavSvazkuBezElevace_DoporuciElevaci()
    {
        var report = Run(Inputs(dirty: VolumeDirtyState.Unknown, isAdmin: false));

        Assert.Contains(report.Findings, f =>
            f.Title.Contains("souborového systému", StringComparison.Ordinal)
            && f.RecommendedAction == DiskDoctorAction.RunAsAdministrator);
    }

    [Fact]
    public void Diagnose_Ssd_NabidneTrimANikdyDefragmentaci()
    {
        var report = Run(Inputs(hasSeekPenalty: false, supportsTrim: true));

        Assert.Contains(report.Findings, f => f.RecommendedAction == DiskDoctorAction.RunTrim);
        Assert.DoesNotContain(report.Findings, f => f.RecommendedAction == DiskDoctorAction.RunDefragment);
    }

    [Fact]
    public void Diagnose_Hdd_NabidneDefragmentaciANikdyTrim()
    {
        var report = Run(Inputs(hasSeekPenalty: true));

        Assert.Contains(report.Findings, f => f.RecommendedAction == DiskDoctorAction.RunDefragment);
        Assert.DoesNotContain(report.Findings, f => f.RecommendedAction == DiskDoctorAction.RunTrim);
    }

    [Fact]
    public void Diagnose_NezjistenyTypDisku_NenabizeneZadnouUdrzbu()
    {
        // Doporučit defragmentaci SSD nebo TRIM na HDD je horší než mlčet -
        // stejné pravidlo jako v okně Optimalizace disku.
        var report = Run(Inputs(hasSeekPenalty: null, supportsTrim: null));

        Assert.DoesNotContain(report.Findings, f =>
            f.RecommendedAction is DiskDoctorAction.RunTrim or DiskDoctorAction.RunDefragment);
    }

    [Fact]
    public void Diagnose_SsdBezTrim_NenavrhujeDefragmentaci()
    {
        var report = Run(Inputs(hasSeekPenalty: false, supportsTrim: false));

        Assert.DoesNotContain(report.Findings, f => f.RecommendedAction == DiskDoctorAction.RunDefragment);
    }

    [Fact]
    public void Diagnose_KazdeZjisteniMaNazevIVysvetleni()
    {
        var report = Run(Inputs(dirty: VolumeDirtyState.Dirty, hasSeekPenalty: true));

        Assert.NotEmpty(report.Findings);
        Assert.All(report.Findings, finding =>
        {
            Assert.False(string.IsNullOrWhiteSpace(finding.Title));
            Assert.False(string.IsNullOrWhiteSpace(finding.Detail));
        });
    }
}

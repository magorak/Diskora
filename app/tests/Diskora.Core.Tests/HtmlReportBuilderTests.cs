using Diskora.Core.Export;
using Diskora.Core.Models;

namespace Diskora.Core.Tests;

public class HtmlReportBuilderTests
{
    private static readonly DateTimeOffset When = new(2026, 7, 28, 14, 30, 0, TimeSpan.FromHours(2));

    private static DiskDoctorReport Report(string subject, params DiskDoctorFinding[] findings) =>
        new(subject, findings.Length == 0 ? DiskDoctorSeverity.Ok : findings.Max(f => f.Severity), findings);

    [Fact]
    public void Build_ObsahujeNazevDiskuIVerdikt()
    {
        var html = HtmlReportBuilder.Build(
            [Report("C: (systém)", new DiskDoctorFinding("Zdraví disku je v pořádku", "Vše sedí.", DiskDoctorSeverity.Ok, DiskDoctorAction.None))],
            When);

        Assert.Contains("C: (systém)", html, StringComparison.Ordinal);
        Assert.Contains("Celkový verdikt: V pořádku", html, StringComparison.Ordinal);
        Assert.Contains("Zdraví disku je v pořádku", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_JeSobestacne_ZadneExterniZdroje()
    {
        // Report musí fungovat offline a nesmí si nikam sáhnout - viz zásada
        // "žádná síťová komunikace" v docs/SECURITY.md.
        var html = HtmlReportBuilder.Build([Report("C:")], When);

        Assert.DoesNotContain("http://", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<style>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_OsetriNebezpecnyNazevSvazku()
    {
        // Popisek svazku si zadává uživatel, takže se nesmí vložit syrový.
        var html = HtmlReportBuilder.Build([Report("<script>alert(1)</script>")], When);

        Assert.DoesNotContain("<script>alert", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_UvedeDoporuceniUNalezuKtereJeMaji()
    {
        var html = HtmlReportBuilder.Build(
            [Report("F:", new DiskDoctorFinding("Chyby přenosu po kabelu: 2426", "Vadný kabel.", DiskDoctorSeverity.Info, DiskDoctorAction.CheckCable))],
            When);

        Assert.Contains("Doporučeno: zkontrolovat datový kabel", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_NalezBezAkceNemaRadekSDoporucenim()
    {
        var html = HtmlReportBuilder.Build(
            [Report("C:", new DiskDoctorFinding("Vše v pořádku", "Nic k řešení.", DiskDoctorSeverity.Ok, DiskDoctorAction.None))],
            When);

        Assert.DoesNotContain("Doporučeno:", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_KritickyNalezDostaneOdlisnouTridu()
    {
        var html = HtmlReportBuilder.Build(
            [Report("D:", new DiskDoctorFinding("Disk hlásí kritický stav", "Zálohujte.", DiskDoctorSeverity.Critical, DiskDoctorAction.BackUpNow))],
            When);

        Assert.Contains("badge crit", html, StringComparison.Ordinal);
        Assert.Contains("Celkový verdikt: Kritické", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_ViceDisku_KazdyMaSvouSekci()
    {
        var html = HtmlReportBuilder.Build([Report("C:"), Report("D:"), Report("E:")], When);

        Assert.Equal(3, html.Split("<section>").Length - 1);
    }

    [Fact]
    public void Build_ZadnyDisk_NespadneAVysvetli()
    {
        var html = HtmlReportBuilder.Build([], When);

        Assert.Contains("Nebyl zkontrolován žádný disk", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_UvadiDatumVytvoreni()
    {
        var html = HtmlReportBuilder.Build([Report("C:")], When);

        Assert.Contains("28. 7. 2026", html, StringComparison.Ordinal);
    }
}

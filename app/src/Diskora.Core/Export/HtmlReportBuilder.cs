using System.Globalization;
using System.Text;
using Diskora.Core.Models;

namespace Diskora.Core.Export;

/// <summary>
/// Sestaví z výsledků Disk Doctora jednu stránku, kterou má smysl poslat
/// příbuznému nebo ITčkaři. Na rozdíl od CSV/JSON exportů, které míří na
/// skriptování, tenhle výstup míří na člověka: srozumitelné věty, barevné
/// odlišení závažnosti, doporučení.
///
/// HTML je zcela soběstačné - styly jsou vložené, žádné obrázky, písma ani
/// skripty zvenčí. Odpovídá to zásadě „žádná síťová komunikace" (viz
/// docs/SECURITY.md): otevřený report si nikam nesáhne, funguje i offline
/// a dá se poslat e-mailem jako jediný soubor.
/// </summary>
public static class HtmlReportBuilder
{
    public static string Build(IReadOnlyList<DiskDoctorReport> reports, DateTimeOffset generatedAt)
    {
        ArgumentNullException.ThrowIfNull(reports);

        var html = new StringBuilder();
        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"cs\">");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine("<title>Zpráva o stavu disků — Diskora</title>");
        html.AppendLine($"<style>{Styles}</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");

        html.AppendLine("<h1>Zpráva o stavu disků</h1>");
        html.AppendLine(
            "<p class=\"meta\">Vytvořeno programem Diskora dne "
            + Escape(generatedAt.ToLocalTime().ToString("d. M. yyyy 'v' H:mm", CultureInfo.GetCultureInfo("cs-CZ")))
            + ".</p>");

        if (reports.Count == 0)
        {
            html.AppendLine("<p>Nebyl zkontrolován žádný disk.</p>");
        }

        foreach (var report in reports)
        {
            AppendReport(html, report);
        }

        html.AppendLine("<p class=\"meta\">Doporučení v této zprávě jsou orientační. "
                        + "U čehokoli označeného jako kritické zálohujte data dřív, než začnete cokoli opravovat.</p>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");
        return html.ToString();
    }

    private static void AppendReport(StringBuilder html, DiskDoctorReport report)
    {
        html.AppendLine("<section>");
        html.AppendLine($"<h2>{Escape(report.Subject)}</h2>");
        html.AppendLine(
            $"<p class=\"verdict {CssClass(report.Overall)}\">Celkový verdikt: {Escape(SeverityText(report.Overall))}</p>");

        if (report.Findings.Count == 0)
        {
            html.AppendLine("<p>Nebylo co zkontrolovat.</p>");
        }

        foreach (var finding in report.Findings)
        {
            html.AppendLine("<div class=\"finding\">");
            html.AppendLine(
                $"<span class=\"badge {CssClass(finding.Severity)}\">{Escape(SeverityText(finding.Severity))}</span>");
            html.AppendLine($"<strong>{Escape(finding.Title)}</strong>");
            html.AppendLine($"<p>{Escape(finding.Detail)}</p>");

            if (ActionText(finding.RecommendedAction) is { } action)
            {
                html.AppendLine($"<p class=\"action\">Doporučeno: {Escape(action)}</p>");
            }

            html.AppendLine("</div>");
        }

        html.AppendLine("</section>");
    }

    /// <summary>
    /// Názvy disků a svazků zadává uživatel (popisky svazků), takže se do HTML
    /// nesmí vkládat syrové - jinak by název jako &lt;script&gt; rozbil stránku.
    /// </summary>
    private static string Escape(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal);

    private static string SeverityText(DiskDoctorSeverity severity) => severity switch
    {
        DiskDoctorSeverity.Ok => "V pořádku",
        DiskDoctorSeverity.Info => "Informace",
        DiskDoctorSeverity.Warning => "Pozor",
        DiskDoctorSeverity.Critical => "Kritické",
        _ => "?",
    };

    private static string CssClass(DiskDoctorSeverity severity) => severity switch
    {
        DiskDoctorSeverity.Ok => "ok",
        DiskDoctorSeverity.Info => "info",
        DiskDoctorSeverity.Warning => "warn",
        _ => "crit",
    };

    private static string? ActionText(DiskDoctorAction action) => action switch
    {
        DiskDoctorAction.BackUpNow => "zálohovat data",
        DiskDoctorAction.RunIntegrityScan => "spustit kontrolu integrity",
        DiskDoctorAction.RunSpotFix => "spustit opravu souborového systému",
        DiskDoctorAction.RunSurfaceScan => "spustit povrchový sken disku",
        DiskDoctorAction.RunTrim => "spustit TRIM",
        DiskDoctorAction.RunDefragment => "spustit defragmentaci",
        DiskDoctorAction.CheckCable => "zkontrolovat datový kabel",
        DiskDoctorAction.RunAsAdministrator => "spustit Diskoru jako administrátor a kontrolu zopakovat",
        _ => null,
    };

    private const string Styles = """
        body { font-family: Segoe UI, Arial, sans-serif; max-width: 46rem; margin: 2rem auto; padding: 0 1rem;
               color: #1c1c1c; line-height: 1.5; }
        h1 { font-size: 1.6rem; margin-bottom: .25rem; }
        h2 { font-size: 1.15rem; margin: 0 0 .5rem; }
        .meta { color: #666; font-size: .875rem; }
        section { border: 1px solid #ddd; border-radius: 8px; padding: 1rem 1.25rem; margin: 1.5rem 0; }
        .verdict { font-weight: 600; padding: .5rem .75rem; border-radius: 6px; color: #fff; display: inline-block; }
        .finding { border-top: 1px solid #eee; padding: .75rem 0; }
        .finding p { margin: .35rem 0 0; }
        .badge { display: inline-block; min-width: 5.5rem; text-align: center; font-size: .75rem;
                 padding: .15rem .5rem; border-radius: 4px; color: #fff; margin-right: .5rem; }
        .action { font-weight: 600; }
        .ok { background: #2e7d32; }
        .info { background: #1565c0; }
        .warn { background: #ef6c00; }
        .crit { background: #c62828; }
        @media (prefers-color-scheme: dark) {
          body { background: #1b1b1b; color: #eee; }
          section { border-color: #3a3a3a; }
          .finding { border-top-color: #333; }
          .meta { color: #aaa; }
        }
        """;
}

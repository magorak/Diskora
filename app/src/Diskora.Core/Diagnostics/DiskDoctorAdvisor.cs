using Diskora.Core.Models;
using Diskora.Core.Smart;

namespace Diskora.Core.Diagnostics;

/// <summary>
/// Převede posbíraná data o disku a svazku na srozumitelný verdikt a doporučení.
/// Čistá funkce bez jakéhokoli I/O - všechno, co je potřeba, dostane v
/// <see cref="DiskDoctorInputs"/>, takže rozhodování jde otestovat bez disků,
/// bez elevace a bez čekání.
///
/// Záměrně nic nespouští ani nenavrhuje "opravit vše" jedním klikem: část
/// doporučení (spotfix, defragmentace) na disk skutečně zapisuje a v Diskoře
/// takové akce vždy patří pod vlastní potvrzení (viz Fáze 3).
/// </summary>
public static class DiskDoctorAdvisor
{
    /// <summary>Atributy, u kterých nenulová hodnota ukazuje na vadné sektory, ne na obecné opotřebení.</summary>
    private static readonly HashSet<byte> BadSectorAttributes = [5, 196, 197, 198];

    /// <summary>Chyby přenosu po sběrnici - vada kabelu, ne disku.</summary>
    private const byte UdmaCrcErrorAttribute = 199;

    public static DiskDoctorReport Diagnose(string subject, DiskDoctorInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        List<DiskDoctorFinding> findings =
        [
            .. DiagnoseSmart(inputs),
            .. DiagnoseIntegrity(inputs),
            .. DiagnoseMaintenance(inputs),
        ];

        var overall = findings.Count == 0
            ? DiskDoctorSeverity.Ok
            : findings.Max(finding => finding.Severity);

        return new DiskDoctorReport(subject, overall, findings);
    }

    private static IEnumerable<DiskDoctorFinding> DiagnoseSmart(DiskDoctorInputs inputs)
    {
        if (!inputs.Smart.IsSupported || inputs.Smart.Report is null)
        {
            yield return inputs.IsRunningAsAdministrator
                ? new DiskDoctorFinding(
                    "Zdraví disku nelze zjistit",
                    "Tenhle disk svoje zdravotní údaje neposkytuje - typicky USB most nebo RAID řadič, " +
                    "který je nepropouští. Není to známka závady, jen se o disku nedá zjistit víc.",
                    DiskDoctorSeverity.Info,
                    DiskDoctorAction.None)
                : new DiskDoctorFinding(
                    "Zdraví disku nelze zjistit bez práv administrátora",
                    "Čtení S.M.A.R.T. u ATA/SATA disků vyžaduje zvýšená oprávnění. Spusťte Diskoru " +
                    "jako administrátor a kontrolu zopakujte - bez toho je tahle část kontroly slepá.",
                    DiskDoctorSeverity.Info,
                    DiskDoctorAction.RunAsAdministrator);

            yield break;
        }

        var report = inputs.Smart.Report;

        yield return report.OverallHealth switch
        {
            DiskHealthStatus.Critical => new DiskDoctorFinding(
                "Disk hlásí kritický stav",
                "Zálohujte data hned, dokud jsou čitelná. Disk překročil hranici, kterou výrobce " +
                "označuje za selhání - další provoz je hazard s daty.",
                DiskDoctorSeverity.Critical,
                DiskDoctorAction.BackUpNow),

            DiskHealthStatus.Warning => new DiskDoctorFinding(
                "Disk hlásí zhoršené hodnoty",
                "Zatím nejde o selhání, ale některá hodnota se zhoršuje. Zálohujte a sledujte, " +
                "jestli se čísla dál posouvají - podrobnosti jsou v okně S.M.A.R.T.",
                DiskDoctorSeverity.Warning,
                DiskDoctorAction.BackUpNow),

            DiskHealthStatus.Healthy => new DiskDoctorFinding(
                "Zdraví disku je v pořádku",
                "Žádná ze sledovaných hodnot nepřekračuje výrobcem daný práh.",
                DiskDoctorSeverity.Ok,
                DiskDoctorAction.None),

            _ => new DiskDoctorFinding(
                "Zdraví disku se nepodařilo vyhodnotit",
                "Disk sice odpověděl, ale nevrátil žádné použitelné hodnoty.",
                DiskDoctorSeverity.Info,
                DiskDoctorAction.None),
        };

        foreach (var finding in DiagnoseAtaAttributes(report))
        {
            yield return finding;
        }

        foreach (var finding in DiagnoseNvme(report))
        {
            yield return finding;
        }
    }

    private static IEnumerable<DiskDoctorFinding> DiagnoseAtaAttributes(SmartReport report)
    {
        foreach (var attribute in report.Attributes)
        {
            var risk = SmartHealthEvaluator.EvaluateAttributeRisk(attribute);

            if (risk != SmartAttributeRisk.Ok)
            {
                yield return new DiskDoctorFinding(
                    $"{SmartAttributeCatalog.GetName(attribute.Id)}: {attribute.RawValue}",
                    SmartAttributeCatalog.GetExplanation(attribute.Id),
                    risk == SmartAttributeRisk.Critical ? DiskDoctorSeverity.Critical : DiskDoctorSeverity.Warning,
                    BadSectorAttributes.Contains(attribute.Id)
                        ? DiskDoctorAction.RunSurfaceScan
                        : DiskDoctorAction.BackUpNow);
            }
            else if (attribute.Id == UdmaCrcErrorAttribute && attribute.RawValue > 0)
            {
                // Tohle by jinak zapadlo: CRC chyby nezhoršují normalizovanou
                // hodnotu, takže disk je "v pořádku", ale přenos dat po kabelu
                // se kazí. Uživateli je to k ničemu, pokud mu to nikdo neřekne.
                yield return new DiskDoctorFinding(
                    $"Chyby přenosu po kabelu: {attribute.RawValue}",
                    "Data se po cestě mezi diskem a základní deskou musela opakovat. Skoro vždy jde " +
                    "o vadný nebo špatně dosedající SATA kabel, ne o vadný disk - zkuste kabel " +
                    "přepojit nebo vyměnit. Číslo je součet za celou dobu života disku a nikdy neklesá, " +
                    "takže samo o sobě neznamená, že problém trvá i teď.",
                    DiskDoctorSeverity.Info,
                    DiskDoctorAction.CheckCable);
            }
        }
    }

    private static IEnumerable<DiskDoctorFinding> DiagnoseNvme(SmartReport report)
    {
        if (report.NvmeHealth is not { } nvmeHealth)
        {
            yield break;
        }

        foreach (var metric in NvmeHealthCatalog.Describe(nvmeHealth))
        {
            if (metric.Risk == SmartAttributeRisk.Ok)
            {
                continue;
            }

            yield return new DiskDoctorFinding(
                $"{metric.Name}: {metric.Value}",
                metric.Explanation,
                metric.Risk == SmartAttributeRisk.Critical ? DiskDoctorSeverity.Critical : DiskDoctorSeverity.Warning,
                DiskDoctorAction.BackUpNow);
        }
    }

    private static IEnumerable<DiskDoctorFinding> DiagnoseIntegrity(DiskDoctorInputs inputs)
    {
        yield return inputs.DirtyState switch
        {
            VolumeDirtyState.Dirty => new DiskDoctorFinding(
                "Souborový systém je označen jako poškozený",
                "Windows si u tohoto svazku poznamenaly, že na něm zbyla neuzavřená změna - typicky " +
                "po výpadku napájení nebo tvrdém restartu. Doporučená je oprava, dokud se problém " +
                "nerozšíří na další soubory.",
                DiskDoctorSeverity.Warning,
                DiskDoctorAction.RunSpotFix),

            VolumeDirtyState.Clean => new DiskDoctorFinding(
                "Souborový systém je bez příznaku poškození",
                "Windows u tohoto svazku neevidují nedokončenou změnu. Hlubší kontrolu (`chkdsk /scan`) " +
                "je i tak možné spustit ručně, je needestruktivní.",
                DiskDoctorSeverity.Ok,
                DiskDoctorAction.None),

            _ => new DiskDoctorFinding(
                "Stav souborového systému nelze zjistit",
                inputs.IsRunningAsAdministrator
                    ? "Svazek na dotaz neodpověděl - může jít o jiný souborový systém než NTFS."
                    : "U systémového nebo zamčeného svazku je tenhle dotaz dostupný jen s právy " +
                      "administrátora. Spusťte Diskoru jako administrátor a kontrolu zopakujte.",
                DiskDoctorSeverity.Info,
                inputs.IsRunningAsAdministrator ? DiskDoctorAction.RunIntegrityScan : DiskDoctorAction.RunAsAdministrator),
        };
    }

    private static IEnumerable<DiskDoctorFinding> DiagnoseMaintenance(DiskDoctorInputs inputs)
    {
        // Při nejistotě se nenabízí nic - stejné pravidlo jako v okně Optimalizace.
        // Doporučit defragmentaci SSD nebo TRIM na HDD je horší než mlčet.
        switch (inputs.Capabilities.IsLikelySolidState)
        {
            case true:
                yield return inputs.Capabilities.SupportsTrim == true
                    ? new DiskDoctorFinding(
                        "Údržba: SSD s podporou TRIM",
                        "Windows spouští TRIM automaticky, takže obvykle není co řešit. Ručně ho jde " +
                        "spustit z okna Optimalizace disku, například po velkém mazání.",
                        DiskDoctorSeverity.Info,
                        DiskDoctorAction.RunTrim)
                    : new DiskDoctorFinding(
                        "Údržba: SSD bez hlášené podpory TRIM",
                        "Defragmentace na SSD nepatří - jen by ho zbytečně opotřebovala bez přínosu " +
                        "pro rychlost. Není tu žádná údržba, kterou byste měli spouštět.",
                        DiskDoctorSeverity.Info,
                        DiskDoctorAction.None);
                break;

            case false:
                yield return new DiskDoctorFinding(
                    "Údržba: mechanický disk",
                    "U talířového disku má smysl defragmentace - poskládá soubory za sebe, aby hlavičky " +
                    "míň skákaly. Nejdřív si můžete nechat spočítat, jak je disk vůbec fragmentovaný.",
                    DiskDoctorSeverity.Info,
                    DiskDoctorAction.RunDefragment);
                break;
        }
    }
}

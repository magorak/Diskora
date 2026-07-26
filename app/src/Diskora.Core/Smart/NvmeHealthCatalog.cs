using System.Globalization;
using Diskora.Core.Formatting;
using Diskora.Core.Models;

namespace Diskora.Core.Smart;

/// <summary>
/// Převádí NVMe health log na srozumitelné řádky pro uživatele - stejný cíl
/// jako <see cref="SmartAttributeCatalog"/> u ATA atributů: ne jen syrová čísla,
/// ale i vysvětlení, co znamenají. Na rozdíl od ATA je sada polí pevně daná
/// specifikací, takže tu není žádný slovník podle ID.
/// </summary>
public static class NvmeHealthCatalog
{
    /// <summary>Popisy jednotlivých bitů pole "critical warning" dle NVMe specifikace.</summary>
    private static readonly (byte Mask, string Description)[] CriticalWarningBits =
    [
        (0x01, "došla rezervní kapacita"),
        (0x02, "teplota mimo povolený rozsah"),
        (0x04, "zhoršená spolehlivost média"),
        (0x08, "médium přepnuto do režimu jen pro čtení"),
        (0x10, "selhala záloha volatilní paměti"),
        (0x20, "perzistentní paměťová oblast je jen pro čtení"),
    ];

    public static IReadOnlyList<NvmeHealthMetric> Describe(NvmeHealthInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        return
        [
            new NvmeHealthMetric(
                "Varování řadiče",
                DescribeCriticalWarning(info.CriticalWarning),
                "Souhrnné hlášení samotného disku. Cokoli jiného než „žádné“ znamená, že disk sám " +
                "oznamuje problém - nejde o odvozený odhad, ale o jeho vlastní verdikt.",
                NvmeHealthEvaluator.EvaluateCriticalWarning(info.CriticalWarning)),

            new NvmeHealthMetric(
                "Spotřebovaná životnost",
                $"{info.PercentageUsed} %",
                "Odhad výrobce, jak velká část zapisovatelné životnosti paměťových buněk je vyčerpaná. " +
                "100 % neznamená okamžité selhání, ale konec garantované životnosti - disk už je za zenitem.",
                NvmeHealthEvaluator.EvaluatePercentageUsed(info.PercentageUsed)),

            new NvmeHealthMetric(
                "Zbývající rezervní kapacita",
                $"{info.AvailableSparePercent} % (práh {info.AvailableSpareThresholdPercent} %)",
                "Náhradní paměťové bloky, kterými disk nahrazuje vadné. Pokles na výrobcem daný práh " +
                "je vážný signál - disk už nemá čím vadné bloky nahrazovat.",
                NvmeHealthEvaluator.EvaluateAvailableSpare(info.AvailableSparePercent, info.AvailableSpareThresholdPercent)),

            new NvmeHealthMetric(
                "Neopravitelné chyby média",
                info.MediaErrors.ToString(CultureInfo.CurrentCulture),
                "Počet případů, kdy disk nedokázal zaručit integritu dat. Nenulová hodnota znamená, " +
                "že už mohlo dojít ke ztrátě dat - zálohujte a disk sledujte.",
                NvmeHealthEvaluator.EvaluateMediaErrors(info.MediaErrors)),

            new NvmeHealthMetric(
                "Teplota",
                FormatTemperature(info.CompositeTemperatureCelsius),
                "Souhrnná provozní teplota řadiče. Trvale vysoká teplota zkracuje životnost; NVMe disky " +
                "se při přehřátí samy zpomalují.",
                NvmeHealthEvaluator.EvaluateTemperature(info.CompositeTemperatureCelsius)),

            new NvmeHealthMetric(
                "Celkem zapsáno",
                ByteSizeFormatter.Format((long)info.BytesWritten),
                "Objem dat zapsaných za celou dobu životnosti disku. Informativní hodnota - porovnáním " +
                "s výrobcem udávanou hodnotou TBW se dá ověřit odhad spotřebované životnosti.",
                SmartAttributeRisk.Ok),

            new NvmeHealthMetric(
                "Celkem přečteno",
                ByteSizeFormatter.Format((long)info.BytesRead),
                "Objem dat přečtených za celou dobu životnosti disku. Čtení paměťové buňky neopotřebovává " +
                "tak jako zápis, jde o čistě informativní hodnotu.",
                SmartAttributeRisk.Ok),

            new NvmeHealthMetric(
                "Doba provozu",
                $"{info.PowerOnHours} h",
                "Celkový počet hodin, kdy byl disk zapnutý. Sama o sobě neznamená problém.",
                SmartAttributeRisk.Ok),

            new NvmeHealthMetric(
                "Počet zapnutí",
                info.PowerCycles.ToString(CultureInfo.CurrentCulture),
                "Kolikrát byl disk zapnut a vypnut. Informativní hodnota.",
                SmartAttributeRisk.Ok),

            new NvmeHealthMetric(
                "Nekorektní vypnutí",
                info.UnsafeShutdowns.ToString(CultureInfo.CurrentCulture),
                "Kolikrát disk přišel o napájení bez řádného odhlášení (výpadek proudu, tvrdý restart). " +
                "Samo o sobě to disk nepoškozuje, ale zvyšuje to riziko poškození souborového systému.",
                SmartAttributeRisk.Ok),

            new NvmeHealthMetric(
                "Záznamy v chybovém logu",
                info.ErrorLogEntryCount.ToString(CultureInfo.CurrentCulture),
                "Kolik chybových událostí disk zaznamenal za celou dobu životnosti. Zahrnuje i drobné, " +
                "automaticky vyřešené chyby, takže nenulová hodnota sama o sobě není poplach.",
                SmartAttributeRisk.Ok),
        ];
    }

    public static string DescribeCriticalWarning(byte criticalWarning)
    {
        if (criticalWarning == 0)
        {
            return "žádné";
        }

        var reasons = CriticalWarningBits
            .Where(bit => (criticalWarning & bit.Mask) != 0)
            .Select(bit => bit.Description)
            .ToList();

        // Nedokumentované/rezervované bity nesmí zmizet beze stopy - kdyby se rozsvítil
        // jen takový bit, hlášení "žádné" by bylo přímo zavádějící.
        if (reasons.Count == 0)
        {
            return $"neznámé varování (0x{criticalWarning:X2})";
        }

        return string.Join(", ", reasons);
    }

    private static string FormatTemperature(double? celsius) =>
        celsius is null ? "nehlášeno" : string.Format(CultureInfo.CurrentCulture, "{0:N0} °C", celsius.Value);
}

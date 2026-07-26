namespace Diskora.App.Settings;

/// <summary>
/// Perzistentní uživatelské předvolby - zatím jen výběr tématu, ale úmyslně
/// vlastní třída (ne přímo `AppTheme` uložené jako string), ať jde snadno
/// přidat další pole (jazyk, práh notifikací - viz Fáze 8 v TODO.md) beze
/// změny formátu existujícího souboru.
/// </summary>
public sealed class AppSettings
{
    public string Theme { get; set; } = "System";

    /// <summary>
    /// Minimální úroveň <see cref="Diskora.Core.Models.DiskHealthStatus"/> (uloženo jako
    /// název hodnoty), od které tray upozorní na zhoršení zdraví disku - viz
    /// <see cref="Tray.DiskHealthNotifier"/>. Výchozí "Warning" odpovídá dosavadnímu
    /// chování (upozornit na cokoliv horšího než předchozí stav).
    /// </summary>
    public string NotificationThreshold { get; set; } = "Warning";

    /// <summary>
    /// Pokud true a Diskora při startu neběží s právy administrátora, nabídne (dialogem
    /// s výchozí volbou "Ne") restart s právy administrátora - viz <c>App.OnStartup</c>.
    /// Výchozí false, ať appka nikoho nenutí do UAC promptu, kdo o to nestojí.
    /// </summary>
    public bool PromptForElevationAtStartup { get; set; }
}

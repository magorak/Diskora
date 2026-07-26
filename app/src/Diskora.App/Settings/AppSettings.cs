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
}

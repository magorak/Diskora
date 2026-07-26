using System.Windows;
using Diskora.App.Settings;
using Microsoft.Win32;

namespace Diskora.App.Theming;

/// <summary>
/// Aplikuje světlé/tmavé téma za běhu přepnutím merged ResourceDictionary na
/// Application úrovni. "System" zjistí aktuální nastavení Windows a mapuje
/// ho na Light/Dark. Volba se přes <see cref="IAppSettingsStore"/> ukládá při
/// každém <see cref="Apply"/> a při startu appky (viz <c>App.OnStartup</c>) se
/// načte zpátky, takže přežije restart - zbytek Fáze 8 (jazyk, práh notifikací,
/// chování elevace) zatím ne.
/// </summary>
public sealed class ThemeService(Application application, IAppSettingsStore settingsStore)
{
    private ResourceDictionary? _activeThemeDictionary;

    public AppTheme Current { get; private set; } = AppTheme.System;

    /// <summary>
    /// Vyvoláno po přepnutí tématu. Prvky, které si barvu z motivu ukládají jako
    /// vypočtenou (ne přes DynamicResource v XAML - typicky interpolované barvy
    /// v kódu, např. buňky treemapy zaplněnosti), se bez tohoto signálu nepřekreslí
    /// a zůstanou vizuálně "zamrzlé" ve starém tématu, dokud je nevynutí jiná událost
    /// (změna dat, resize).
    /// </summary>
    public event Action? ThemeChanged;

    public void Apply(AppTheme theme)
    {
        Current = theme;
        var resolvedIsLight = theme switch
        {
            AppTheme.Light => true,
            AppTheme.Dark => false,
            _ => DetectSystemThemeIsLight(),
        };

        var uri = new Uri(resolvedIsLight ? "Themes/Light.xaml" : "Themes/Dark.xaml", UriKind.Relative);
        var dictionary = new ResourceDictionary { Source = uri };

        if (_activeThemeDictionary is not null)
        {
            application.Resources.MergedDictionaries.Remove(_activeThemeDictionary);
        }

        application.Resources.MergedDictionaries.Insert(0, dictionary);
        _activeThemeDictionary = dictionary;

        var settings = settingsStore.Load();
        settings.Theme = theme.ToString();
        settingsStore.Save(settings);

        ThemeChanged?.Invoke();
    }

    /// <summary>Načte uloženou volbu tématu - neplatná/chybějící hodnota v souboru tiše spadne na System.</summary>
    public static AppTheme LoadSavedTheme(IAppSettingsStore settingsStore) =>
        Enum.TryParse<AppTheme>(settingsStore.Load().Theme, out var theme) ? theme : AppTheme.System;

    private static bool DetectSystemThemeIsLight()
    {
        const string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath);
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int intValue ? intValue != 0 : true;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException)
        {
            return true;
        }
    }
}

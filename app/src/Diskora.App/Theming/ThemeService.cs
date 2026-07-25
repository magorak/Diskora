using System.Windows;
using Microsoft.Win32;

namespace Diskora.App.Theming;

/// <summary>
/// Aplikuje světlé/tmavé téma za běhu přepnutím merged ResourceDictionary na
/// Application úrovni. "System" zjistí aktuální nastavení Windows a mapuje
/// ho na Light/Dark - preference se zatím nepersistuje mezi spuštěními
/// (viz Fáze 8 v TODO.md - perzistence nastavení).
/// </summary>
public sealed class ThemeService(Application application)
{
    private ResourceDictionary? _activeThemeDictionary;

    public AppTheme Current { get; private set; } = AppTheme.System;

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
    }

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

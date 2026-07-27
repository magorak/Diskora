using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using Diskora.App.ViewModels;

namespace Diskora.App;

public partial class WhatsNewWindow : Window
{
    /// <summary>
    /// Webová verze téhož changelogu (generuje se z téhož `CHANGELOG.md` při
    /// buildu webu, viz `web/src/pages/docs/changelog.astro`).
    /// </summary>
    private const string WebChangelogUrl = "https://www.magorak.cz/diskora/docs/changelog/";

    public WhatsNewWindow()
    {
        InitializeComponent();
        DataContext = new WhatsNewViewModel(ReadEmbeddedChangelog(), CurrentVersion);
    }

    /// <summary>Verze sestavení zkrácená na major.minor.patch - stejný tvar jako v dialogu „O aplikaci".</summary>
    public static string CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";

    private static string ReadEmbeddedChangelog()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Diskora.App.CHANGELOG.md");
        if (stream is null)
        {
            return string.Empty;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private void OpenOnWeb_Click(object sender, RoutedEventArgs e)
    {
        // Diskora sama nikam nechodí (žádný HttpClient, viz docs/SECURITY.md) -
        // jen předá adresu výchozímu prohlížeči, a to výhradně na kliknutí uživatele.
        try
        {
            Process.Start(new ProcessStartInfo(WebChangelogUrl) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            MessageBox.Show(
                this,
                $"Odkaz se nepodařilo otevřít. Adresa je:\n\n{WebChangelogUrl}",
                "Otevření odkazu selhalo",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

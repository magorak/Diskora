using System.Text;
using System.Windows;
using System.Windows.Threading;
using Diskora.App.Settings;
using Diskora.App.Theming;
using Diskora.Data;

namespace Diskora.App;

public partial class App : Application
{
    public ThemeService Theme { get; private set; } = null!;

    public IAppSettingsStore SettingsStore { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        SettingsStore = new JsonAppSettingsStore();
        Theme = new ThemeService(this, SettingsStore);
        Theme.Apply(ThemeService.LoadSavedTheme(SettingsStore));

        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, WarnAboutLeftoverAttachments);
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, OfferElevationRestartIfRequested);
    }

    /// <summary>
    /// Pokud uživatel v Nastavení zapnul <see cref="AppSettings.PromptForElevationAtStartup"/>
    /// a Diskora zrovna neběží s právy administrátora, nabídne restart s elevací -
    /// výchozí tlačítko dialogu je záměrně "Ne" (stejný bezpečnostní vzor jako u
    /// potvrzení spotfixu ve Fázi 3), ať appka nikoho nenutí do UAC promptu omylem.
    /// Odmítnutí uživatelem UAC dialogem (Win32 chyba 1223) se tiše ignoruje - appka
    /// prostě pokračuje bez elevace, žádný pád.
    /// </summary>
    private void OfferElevationRestartIfRequested()
    {
        if (Diskora.Native.ElevationHelper.IsRunningAsAdministrator())
        {
            return;
        }

        if (!SettingsStore.Load().PromptForElevationAtStartup)
        {
            return;
        }

        var confirmed = MessageBox.Show(
            "Diskora zrovna neběží s právy administrátora - oprava disku, TRIM/defrag a " +
            "S.M.A.R.T. proto nebudou dostupné. Restartovat Diskoru s právy administrátora?",
            "Restartovat s právy administrátora",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No) == MessageBoxResult.Yes;

        if (!confirmed)
        {
            return;
        }

        try
        {
            var exePath = Environment.ProcessPath;
            if (exePath is null)
            {
                return;
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exePath)
            {
                UseShellExecute = true,
                Verb = "runas",
            });
            Shutdown();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Uživatel zrušil UAC prompt (chyba 1223) nebo elevace jinak selhala -
            // Diskora dál běží bez elevace, žádné další hlášení navíc není potřeba.
        }
    }

    /// <summary>
    /// Diskora připojuje VHD/VHDX s ATTACH_VIRTUAL_DISK_FLAG_PERMANENT_LIFETIME (viz
    /// VirtualDiskAttacher), takže disk zůstává připojený i po pádu/zavření aplikace
    /// bez explicitního odpojení. Při dalším startu na to upozorníme, ať uživatel
    /// nezapomene odpojit něco, co už nepotřebuje - samotné odpojení se dělá stále
    /// jen ručně z okna "Otevřít virtuální disk", tady jde jen o informaci.
    /// </summary>
    private static void WarnAboutLeftoverAttachments()
    {
        var registry = new SqliteVirtualDiskAttachmentRegistry();
        var leftovers = registry.GetTrackedAttachments();
        if (leftovers.Count == 0)
        {
            return;
        }

        var message = new StringBuilder()
            .AppendLine("Z předchozího spuštění Diskory zůstaly připojené tyto virtuální disky/obrazy:")
            .AppendLine();

        foreach (var entry in leftovers)
        {
            message.AppendLine($"  • {entry.Path}");
        }

        message.AppendLine()
            .Append("Pokud je už nepotřebujete, otevřete je přes „Soubor -> Otevřít virtuální disk / ISO...“ a odpojte.");

        MessageBox.Show(
            message.ToString(),
            "Připojené virtuální disky",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}

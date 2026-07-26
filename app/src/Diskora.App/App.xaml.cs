using System.Text;
using System.Windows;
using System.Windows.Threading;
using Diskora.App.Theming;
using Diskora.Data;

namespace Diskora.App;

public partial class App : Application
{
    public ThemeService Theme { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Theme = new ThemeService(this);
        Theme.Apply(AppTheme.System);

        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, WarnAboutLeftoverAttachments);
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

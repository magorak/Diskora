using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Diskora.App.Tray;
using Diskora.App.ViewModels;
using Diskora.Core.Services;

namespace Diskora.App;

public partial class MainWindow : Window
{
    private readonly TrayIconService _trayIcon;
    private bool _hasShownMinimizeHint;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new DashboardViewModel(new DiskEnumerationService(), ((App)Application.Current).Theme);

        _trayIcon = new TrayIconService(this);
        _trayIcon.Show();
        StateChanged += MainWindow_StateChanged;
        Closed += (_, _) => _trayIcon.Dispose();
    }

    /// <summary>
    /// Tray ikona je vidět po celou dobu běhu (ne jen po minimalizaci) - připravuje to
    /// půdu pro budoucí upozornění na zhoršení zdraví disku (Fáze 2), která se mají
    /// zobrazit bez ohledu na to, jestli je hlavní okno zrovna otevřené. Minimalizace
    /// navíc okno schová úplně (Hide, ne jen ikonu na hlavním panelu) - zavření (×)
    /// naopak aplikaci normálně ukončí (stejně jako menu Soubor -> Konec), ať
    /// uživatele nepřekvapí zdánlivě "zmizelá" aplikace. Balónková nápověda se ukáže
    /// jen při prvním schování za běh, ne opakovaně při každé minimalizaci.
    /// </summary>
    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState != WindowState.Minimized)
        {
            return;
        }

        Hide();

        if (!_hasShownMinimizeHint)
        {
            _hasShownMinimizeHint = true;
            _trayIcon.ShowBalloonTip("Diskora běží na pozadí", "Diskoru znovu otevřete kliknutím na tuto ikonu.");
        }
    }

    private void ShowSmart_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: PhysicalDiskRowViewModel disk })
        {
            return;
        }

        var smartWindow = new SmartWindow(disk.Index, disk.FriendlyName)
        {
            Owner = this,
        };
        smartWindow.Show();
    }

    private void ShowSurfaceScan_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: PhysicalDiskRowViewModel disk })
        {
            return;
        }

        var surfaceScanWindow = new SurfaceScanWindow(disk.Index, disk.FriendlyName, (long)disk.SizeBytes)
        {
            Owner = this,
        };
        surfaceScanWindow.Show();
    }

    private void ShowIntegrity_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: VolumeRowViewModel volume })
        {
            return;
        }

        var integrityWindow = new IntegrityWindow(volume.Name, $"{volume.Name} ({volume.Label})")
        {
            Owner = this,
        };
        integrityWindow.Show();
    }

    private void ShowDiskUsage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: VolumeRowViewModel volume })
        {
            return;
        }

        var diskUsageWindow = new DiskUsageWindow(volume.Name)
        {
            Owner = this,
        };
        diskUsageWindow.Show();
    }

    private void AnalyzeFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Vybrat složku k analýze zaplněnosti",
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var diskUsageWindow = new DiskUsageWindow(dialog.FolderName)
        {
            Owner = this,
        };
        diskUsageWindow.Show();
    }

    private void OpenVirtualDisk_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Otevřít virtuální disk nebo obraz",
            Filter = "Virtuální disky a obrazy (*.vhd;*.vhdx;*.iso;*.img;*.raw;*.dd)|*.vhd;*.vhdx;*.iso;*.img;*.raw;*.dd|Všechny soubory (*.*)|*.*",
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var virtualDiskWindow = new VirtualDiskWindow(dialog.FileName)
        {
            Owner = this,
        };
        virtualDiskWindow.Show();
    }

    private void ShowOptimization_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: VolumeRowViewModel volume })
        {
            return;
        }

        var optimizationWindow = new OptimizationWindow(volume.Name, $"{volume.Name} ({volume.Label})")
        {
            Owner = this,
        };
        optimizationWindow.Show();
    }

    private void ShowSystemEventLog_Click(object sender, RoutedEventArgs e)
    {
        var window = new SystemEventLogWindow
        {
            Owner = this,
        };
        window.Show();
    }

    private void ShowAbout_Click(object sender, RoutedEventArgs e)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";
        MessageBox.Show(
            this,
            $"Diskora {version}\n\nOpen-source nástroj pro kontrolu, opravu a analýzu disků.\n" +
            "Licencováno pod GNU GPLv3. Bez telemetrie.",
            "O aplikaci Diskora",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}

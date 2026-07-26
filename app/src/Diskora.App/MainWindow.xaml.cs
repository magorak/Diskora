using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Diskora.App.ViewModels;
using Diskora.Core.Services;

namespace Diskora.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new DashboardViewModel(new DiskEnumerationService(), ((App)Application.Current).Theme);
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
            Title = "Otevřít virtuální disk nebo ISO obraz",
            Filter = "Virtuální disky a obrazy (*.vhd;*.vhdx;*.iso)|*.vhd;*.vhdx;*.iso|Všechny soubory (*.*)|*.*",
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

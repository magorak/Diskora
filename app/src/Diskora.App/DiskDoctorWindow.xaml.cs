using System.Windows;
using System.Windows.Controls;
using Diskora.App.Export;
using Diskora.App.ViewModels;
using Diskora.Core.Export;
using Diskora.Core.Models;
using Diskora.Core.Services;
using Diskora.Data;
using Diskora.Native;
using Diskora.Repair;

namespace Diskora.App;

public partial class DiskDoctorWindow : Window
{
    private readonly string _driveLetter;
    private readonly string _volumeSubject;
    private readonly int? _physicalDiskIndex;
    private readonly string _diskName;
    private readonly long _diskSizeBytes;

    public DiskDoctorWindow(string driveLetter, string volumeSubject, int? physicalDiskIndex, string diskName, long diskSizeBytes)
    {
        InitializeComponent();

        _driveLetter = driveLetter;
        _volumeSubject = volumeSubject;
        _physicalDiskIndex = physicalDiskIndex;
        _diskName = diskName;
        _diskSizeBytes = diskSizeBytes;

        var doctorService = new DiskDoctorService(
            new SmartService(new SqliteDiskHistoryStore()),
            new IntegrityCheckService(),
            new DiskOptimizationService(),
            ElevationHelper.IsRunningAsAdministrator);

        var viewModel = new DiskDoctorViewModel(doctorService, driveLetter, physicalDiskIndex, volumeSubject);
        DataContext = viewModel;

        // Kontrola je needestruktivní, takže se spustí sama - uživatel chtěl
        // "jedno tlačítko", ne "otevřít okno a pak ještě někde kliknout".
        Loaded += async (_, _) => await viewModel.RunAsync();
    }

    private void SaveReport_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DiskDoctorViewModel viewModel || !viewModel.HasRun)
        {
            return;
        }

        var report = new DiskDoctorReport(
            viewModel.Subject,
            viewModel.Overall,
            viewModel.Findings
                .Select(f => new DiskDoctorFinding(f.Title, f.Detail, f.Severity, f.Action))
                .ToList());

        ExportHelper.SaveHtmlReport(
            this,
            HtmlReportBuilder.Build([report], DateTimeOffset.Now),
            // Písmeno chodí ve tvaru "C:\" - dvojtečka i lomítko musí pryč, jinak
            // vznikne nepoužitelný název souboru a uložení selže (nahlásil uživatel).
            $"diskora-zprava-{_driveLetter.Trim('\\', ':')}.html");
    }

    private void Action_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: DiskDoctorFindingRowViewModel row })
        {
            return;
        }

        // Disk Doctor sám nic nespouští - jen otevře příslušné okno, kde má
        // akce svoje vlastní potvrzení (spotfix a defragmentace zapisují na disk).
        Window? target = row.Action switch
        {
            DiskDoctorAction.RunIntegrityScan or DiskDoctorAction.RunSpotFix =>
                new IntegrityWindow(_driveLetter, _volumeSubject),

            DiskDoctorAction.RunTrim or DiskDoctorAction.RunDefragment =>
                new OptimizationWindow(_driveLetter, _volumeSubject),

            DiskDoctorAction.RunSurfaceScan when _physicalDiskIndex is { } index =>
                new SurfaceScanWindow(index, _diskName, _diskSizeBytes),

            _ => null,
        };

        if (target is null)
        {
            return;
        }

        target.Owner = this;
        target.Show();
    }
}

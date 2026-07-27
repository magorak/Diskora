using System.Windows;
using System.Windows.Controls;
using Diskora.App.ViewModels;
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

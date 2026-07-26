using System.Windows;
using Diskora.App.Export;
using Diskora.App.ViewModels;
using Diskora.Core.Export;
using Diskora.Core.Services;
using Diskora.Data;

namespace Diskora.App;

public partial class IntegrityWindow : Window
{
    public IntegrityWindow(string driveLetter, string volumeName)
    {
        InitializeComponent();
        var historyStore = new SqliteDiskHistoryStore();
        DataContext = new IntegrityViewModel(new IntegrityCheckService(historyStore), historyStore, driveLetter, volumeName);
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not IntegrityViewModel viewModel)
        {
            return;
        }

        var csv = CsvWriter.Write(
            ["Kdy", "Stav", "Sken"],
            viewModel.History.Select(h => (IReadOnlyList<string>)
                [h.TimestampDisplay, h.DirtyStateDisplay, h.ScanDisplay]));

        ExportHelper.SaveCsv(this, csv, "diskora-integrita.csv");
    }

    private void ExportJson_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not IntegrityViewModel viewModel)
        {
            return;
        }

        var payload = new
        {
            viewModel.VolumeName,
            DirtyState = viewModel.DirtyStateDisplay,
            History = viewModel.History.Select(h => new { h.TimestampDisplay, DirtyState = h.DirtyStateDisplay, Scan = h.ScanDisplay }),
        };

        ExportHelper.SaveJson(this, payload, "diskora-integrita.json");
    }
}

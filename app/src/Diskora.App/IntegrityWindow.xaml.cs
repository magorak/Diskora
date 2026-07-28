using System.Windows;
using System.Windows.Controls;
using Diskora.App.Export;
using Diskora.App.ViewModels;
using Diskora.Core.Export;
using Diskora.Core.Services;
using Diskora.Data;

namespace Diskora.App;

public partial class IntegrityWindow : Window
{
    /// <summary>
    /// Dokud uživatel neodroluje sám nahoru, výpis se drží na posledním řádku -
    /// jinak by musel při běžící kontrole rolovat pořád ručně. Jakmile odroluje
    /// nahoru (chce si přečíst starší řádek), sledování se vypne a zapne se zpátky,
    /// až se vrátí na konec.
    /// </summary>
    private bool _followOutputTail = true;

    public IntegrityWindow(string driveLetter, string volumeName)
    {
        InitializeComponent();
        var historyStore = new SqliteDiskHistoryStore();
        DataContext = new IntegrityViewModel(new IntegrityCheckService(historyStore), historyStore, driveLetter, volumeName);
    }

    private void OutputScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        // Změna výšky obsahu = přibyl řádek. Změna svislé pozice bez toho = roloval uživatel.
        if (e.ExtentHeightChange == 0)
        {
            const double tolerance = 1.0;
            _followOutputTail = scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - tolerance;
            return;
        }

        if (_followOutputTail)
        {
            scrollViewer.ScrollToEnd();
        }
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

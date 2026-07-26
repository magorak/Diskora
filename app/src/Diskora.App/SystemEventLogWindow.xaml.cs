using System.Windows;
using Diskora.App.Export;
using Diskora.App.ViewModels;
using Diskora.Core.Export;
using Diskora.Core.Services;

namespace Diskora.App;

public partial class SystemEventLogWindow : Window
{
    public SystemEventLogWindow()
    {
        InitializeComponent();
        DataContext = new SystemEventLogViewModel(new DiskEventLogService());
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SystemEventLogViewModel viewModel)
        {
            return;
        }

        var csv = CsvWriter.Write(
            ["Kdy", "Úroveň", "Protokol", "Zdroj", "ID", "Popis"],
            viewModel.Entries.Select(entry => (IReadOnlyList<string>)
                [entry.TimeDisplay, entry.LevelDisplay, entry.LogName, entry.ProviderName, entry.EventId.ToString(), entry.Message]));

        ExportHelper.SaveCsv(this, csv, "diskora-systemovy-protokol.csv");
    }

    private void ExportJson_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SystemEventLogViewModel viewModel)
        {
            return;
        }

        var payload = viewModel.Entries.Select(entry => new
        {
            entry.TimeDisplay,
            Level = entry.LevelDisplay,
            entry.LogName,
            entry.ProviderName,
            entry.EventId,
            entry.Message,
        });

        ExportHelper.SaveJson(this, payload, "diskora-systemovy-protokol.json");
    }
}

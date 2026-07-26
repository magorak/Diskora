using System.Windows;
using Diskora.App.Export;
using Diskora.App.ViewModels;
using Diskora.Core.Export;
using Diskora.Core.Services;
using Diskora.Data;

namespace Diskora.App;

public partial class SmartWindow : Window
{
    public SmartWindow(int diskIndex, string diskName)
    {
        InitializeComponent();
        var historyStore = new SqliteDiskHistoryStore();
        DataContext = new SmartViewModel(new SmartService(historyStore), historyStore, diskIndex, diskName);
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SmartViewModel viewModel)
        {
            return;
        }

        var csv = viewModel.IsNvme
            ? CsvWriter.Write(
                ["Údaj", "Hodnota", "Riziko"],
                viewModel.NvmeMetrics.Select(m => (IReadOnlyList<string>)[m.Name, m.Value, m.RiskDisplay]))
            : CsvWriter.Write(
                ["ID", "Atribut", "Aktuální", "Nejhorší", "Práh", "Raw", "Riziko"],
                viewModel.Attributes.Select(a => (IReadOnlyList<string>)
                    [a.Id.ToString(), a.Name, a.CurrentValue.ToString(), a.WorstValue.ToString(), a.Threshold.ToString(), a.RawValue.ToString(), a.RiskDisplay]));

        ExportHelper.SaveCsv(this, csv, "diskora-smart.csv");
    }

    private void ExportJson_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SmartViewModel viewModel)
        {
            return;
        }

        var payload = new
        {
            viewModel.DiskName,
            viewModel.IsSupported,
            viewModel.IsNvme,
            viewModel.UnavailableReason,
            OverallHealth = viewModel.OverallHealthDisplay,
            NvmeMetrics = viewModel.NvmeMetrics.Select(m => new
            {
                m.Name,
                m.Value,
                Risk = m.RiskDisplay,
                m.Explanation,
            }),
            Attributes = viewModel.Attributes.Select(a => new
            {
                a.Id,
                a.Name,
                a.CurrentValue,
                a.WorstValue,
                a.Threshold,
                a.RawValue,
                Risk = a.RiskDisplay,
                a.Explanation,
            }),
            History = viewModel.History.Select(h => new { h.TimestampDisplay, Health = h.HealthDisplay }),
        };

        ExportHelper.SaveJson(this, payload, "diskora-smart.json");
    }
}

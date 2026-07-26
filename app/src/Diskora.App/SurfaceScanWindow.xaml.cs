using System.Windows;
using Diskora.App.Export;
using Diskora.App.ViewModels;
using Diskora.Core.Export;
using Diskora.Core.Formatting;
using Diskora.Core.Services;

namespace Diskora.App;

public partial class SurfaceScanWindow : Window
{
    public SurfaceScanWindow(int physicalDiskIndex, string diskName, long sizeBytes)
    {
        InitializeComponent();
        DataContext = new SurfaceScanViewModel(new SurfaceScanService(), physicalDiskIndex, diskName, sizeBytes);
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SurfaceScanViewModel viewModel)
        {
            return;
        }

        var csv = CsvWriter.Write(
            ["Od (B)", "Od", "Do (B)", "Do"],
            viewModel.BadRanges.Select(r => (IReadOnlyList<string>)
                [r.OffsetBytes.ToString(), ByteSizeFormatter.Format(r.OffsetBytes),
                 (r.OffsetBytes + r.LengthBytes).ToString(), ByteSizeFormatter.Format(r.OffsetBytes + r.LengthBytes)]));

        ExportHelper.SaveCsv(this, csv, "diskora-povrchovy-sken.csv");
    }

    private void ExportJson_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SurfaceScanViewModel viewModel)
        {
            return;
        }

        var payload = new
        {
            viewModel.DiskName,
            viewModel.Summary,
            BadRanges = viewModel.BadRanges.Select(r => new
            {
                r.OffsetBytes,
                r.LengthBytes,
                EndOffsetBytes = r.OffsetBytes + r.LengthBytes,
            }),
        };

        ExportHelper.SaveJson(this, payload, "diskora-povrchovy-sken.json");
    }
}

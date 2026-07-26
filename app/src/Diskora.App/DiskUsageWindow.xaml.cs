using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Diskora.App.ViewModels;
using Diskora.Core.Export;
using Diskora.Core.Services;

namespace Diskora.App;

public partial class DiskUsageWindow : Window
{
    public DiskUsageWindow(string rootPath)
    {
        InitializeComponent();
        var viewModel = new DiskUsageViewModel(new DiskUsageScanner(), new DuplicateFileFinder(), rootPath);
        viewModel.CompositionSegments.CollectionChanged += (_, _) => RebuildCompositionBar(viewModel);
        DataContext = viewModel;
    }

    private void ItemsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemsGrid.SelectedItem is DiskUsageNodeRowViewModel row && DataContext is DiskUsageViewModel viewModel)
        {
            viewModel.NavigateInto(row);
        }
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DiskUsageViewModel viewModel)
        {
            return;
        }

        string csv;
        string suggestedName;
        switch (ResultsTabControl.SelectedIndex)
        {
            case 1:
                csv = CsvWriter.Write(
                    ["Název", "Velikost (B)", "Velikost", "Poslední změna", "Umístění"],
                    viewModel.LargestFiles.Select(f => (IReadOnlyList<string>)
                        [f.Name, f.SizeBytes.ToString(), f.SizeDisplay, f.LastWriteDisplay, f.FullPath]));
                suggestedName = "diskora-nejvetsi-soubory.csv";
                break;
            case 2:
                csv = CsvWriter.Write(
                    ["Název", "Velikost (B)", "Velikost", "Poslední změna", "Umístění"],
                    viewModel.OldestFiles.Select(f => (IReadOnlyList<string>)
                        [f.Name, f.SizeBytes.ToString(), f.SizeDisplay, f.LastWriteDisplay, f.FullPath]));
                suggestedName = "diskora-nejstarsi-soubory.csv";
                break;
            case 3:
                csv = CsvWriter.Write(
                    ["Skupina", "Velikost (B)", "Velikost", "Umístění"],
                    viewModel.DuplicateFiles.Select(f => (IReadOnlyList<string>)
                        [f.GroupNumber.ToString(), f.SizeBytes.ToString(), f.SizeDisplay, f.FullPath]));
                suggestedName = "diskora-duplicity.csv";
                break;
            default:
                csv = CsvWriter.Write(
                    ["Název", "Velikost (B)", "Velikost", "Podíl (%)", "Souborů", "Stav", "Umístění"],
                    viewModel.Items.Select(i => (IReadOnlyList<string>)
                        [i.Name, i.Node.SizeBytes.ToString(), i.SizeDisplay, i.PercentOfParent.ToString("F1"), i.FileCount.ToString(), i.StatusDisplay, i.Node.FullPath]));
                suggestedName = "diskora-slozky.csv";
                break;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Exportovat do CSV",
            Filter = "CSV soubor (*.csv)|*.csv|Všechny soubory (*.*)|*.*",
            FileName = suggestedName,
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            File.WriteAllText(dialog.FileName, csv, System.Text.Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Export se nepodařilo uložit: {ex.Message}", "Export CSV",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static readonly JsonSerializerOptions JsonExportOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Latin1Supplement, UnicodeRanges.LatinExtendedA),
    };

    private void ExportJson_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DiskUsageViewModel viewModel)
        {
            return;
        }

        object payload;
        string suggestedName;
        switch (ResultsTabControl.SelectedIndex)
        {
            case 1:
                payload = viewModel.LargestFiles.Select(f => new
                {
                    f.Name,
                    SizeBytes = f.SizeBytes,
                    f.SizeDisplay,
                    LastWriteTimeUtc = f.LastWriteTimeUtc,
                    f.FullPath,
                });
                suggestedName = "diskora-nejvetsi-soubory.json";
                break;
            case 2:
                payload = viewModel.OldestFiles.Select(f => new
                {
                    f.Name,
                    SizeBytes = f.SizeBytes,
                    f.SizeDisplay,
                    LastWriteTimeUtc = f.LastWriteTimeUtc,
                    f.FullPath,
                });
                suggestedName = "diskora-nejstarsi-soubory.json";
                break;
            case 3:
                payload = viewModel.DuplicateFiles
                    .GroupBy(f => f.GroupNumber)
                    .Select(g => new
                    {
                        Skupina = g.Key,
                        SizeBytes = g.First().SizeBytes,
                        Soubory = g.Select(f => f.FullPath).ToList(),
                    });
                suggestedName = "diskora-duplicity.json";
                break;
            default:
                payload = viewModel.Items.Select(i => new
                {
                    i.Name,
                    SizeBytes = i.Node.SizeBytes,
                    i.SizeDisplay,
                    PercentOfParent = i.PercentOfParent,
                    FileCount = i.FileCount,
                    i.StatusDisplay,
                    FullPath = i.Node.FullPath,
                });
                suggestedName = "diskora-slozky.json";
                break;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Exportovat do JSON",
            Filter = "JSON soubor (*.json)|*.json|Všechny soubory (*.*)|*.*",
            FileName = suggestedName,
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(payload, JsonExportOptions);
            File.WriteAllText(dialog.FileName, json, System.Text.Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Export se nepodařilo uložit: {ex.Message}", "Export JSON",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// Vodorovný kompoziční pruh (viz skill dataviz - part-to-whole formou
    /// segmentovaného pruhu, ne koláčem: u mnoha/dlouhých názvů složek se
    /// koláčové výseče špatně porovnávají). ColumnDefinitions je potřeba
    /// stavět v kódu, protože na rozdíl od ItemsControl.ItemsSource nejde
    /// dynamickou kolekci navázat přímo na Grid.ColumnDefinitions v XAML -
    /// GridLength(Star) ale i tak dá přesné proporcionální šířky bez ruční
    /// práce s pixely.
    /// </summary>
    private void RebuildCompositionBar(DiskUsageViewModel viewModel)
    {
        CompositionBarGrid.ColumnDefinitions.Clear();
        CompositionBarGrid.Children.Clear();

        var segments = viewModel.CompositionSegments;
        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            CompositionBarGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(Math.Max(segment.Percent, 0.01), GridUnitType.Star),
            });

            var brushKey = segment.SeriesIndex is >= 0 and <= 4 ? $"SeriesBrush{segment.SeriesIndex + 1}" : "MutedForegroundBrush";
            var border = new Border
            {
                Background = (Brush)FindResource(brushKey),
                Margin = i < segments.Count - 1 ? new Thickness(0, 0, 2, 0) : new Thickness(0),
                ToolTip = segment.TooltipText,
            };
            Grid.SetColumn(border, i);
            CompositionBarGrid.Children.Add(border);
        }
    }
}

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
using Diskora.Core.Layout;
using Diskora.Core.Services;

namespace Diskora.App;

public partial class DiskUsageWindow : Window
{
    public DiskUsageWindow(string rootPath)
    {
        InitializeComponent();
        var viewModel = new DiskUsageViewModel(new DiskUsageScanner(), new DuplicateFileFinder(), rootPath);
        viewModel.CompositionSegments.CollectionChanged += (_, _) => RebuildCompositionBar(viewModel);
        viewModel.TreemapCells.CollectionChanged += (_, _) => RebuildTreemap(viewModel);
        DataContext = viewModel;

        // Barvy obou prvků (kompoziční pruh i treemapa) se počítají v kódu, ne přes
        // DynamicResource v XAML, takže se samy nepřekreslí při přepnutí tématu za běhu -
        // bez tohoto se signálem zůstanou "zamrzlé" ve starém tématu (živě odhaleno).
        var theme = ((App)Application.Current).Theme;
        void OnThemeChanged()
        {
            RebuildCompositionBar(viewModel);
            RebuildTreemap(viewModel);
        }

        theme.ThemeChanged += OnThemeChanged;
        Closed += (_, _) => theme.ThemeChanged -= OnThemeChanged;
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

    private void TreemapCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (DataContext is DiskUsageViewModel viewModel)
        {
            RebuildTreemap(viewModel);
        }
    }

    private static readonly Color TreemapDarkInk = Color.FromRgb(0x0B, 0x0B, 0x0B);

    /// <summary>
    /// Barva buňky kóduje velikost (sekvenční jedna barva, světlá→tmavá dle podílu -
    /// viz skill dataviz: buňky nejsou pojmenované kategorie, takže kategoriální paleta
    /// by sem neseděla). Popisek se vykresluje přímo do vyplněné buňky - podle skillu
    /// je "label uvnitř barevné výplně" jediná výjimka z pravidla "text nikdy nenese
    /// barvu dat": barva textu (bílá/tmavá) se volí podle jasu výplně, aby vždy byla čitelná.
    /// </summary>
    private void RebuildTreemap(DiskUsageViewModel viewModel)
    {
        TreemapCanvas.Children.Clear();

        var width = TreemapCanvas.ActualWidth;
        var height = TreemapCanvas.ActualHeight;
        var cells = viewModel.TreemapCells;
        if (width <= 0 || height <= 0 || cells.Count == 0)
        {
            return;
        }

        var weights = cells.Select(c => (double)c.SizeBytes).ToList();
        var rects = SquarifiedTreemapLayout.Layout(weights, 0, 0, width, height);

        var minSize = cells.Min(c => c.SizeBytes);
        var maxSize = cells.Max(c => c.SizeBytes);
        var lowColor = ((SolidColorBrush)FindResource("TreemapCellLowBrush")).Color;
        var highColor = ((SolidColorBrush)FindResource("TreemapCellHighBrush")).Color;

        const double gap = 2;

        for (var i = 0; i < cells.Count; i++)
        {
            var rect = rects[i];
            if (rect.Width <= gap || rect.Height <= gap)
            {
                continue;
            }

            var cell = cells[i];
            var ratio = maxSize > minSize ? (double)(cell.SizeBytes - minSize) / (maxSize - minSize) : 1.0;
            var fillColor = LerpColor(lowColor, highColor, ratio);

            var border = new Border
            {
                Width = rect.Width - gap,
                Height = rect.Height - gap,
                Background = new SolidColorBrush(fillColor),
                CornerRadius = new CornerRadius(2),
                ToolTip = cell.TooltipText,
                Cursor = cell.IsNavigable ? Cursors.Hand : Cursors.Arrow,
            };

            if (border.Width >= 36 && border.Height >= 20)
            {
                border.Child = new TextBlock
                {
                    Text = cell.Name,
                    Foreground = new SolidColorBrush(RelativeLuminance(fillColor) > 0.55 ? TreemapDarkInk : Colors.White),
                    FontSize = 11,
                    Margin = new Thickness(4, 2, 4, 2),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                };
            }

            if (cell.IsNavigable)
            {
                border.MouseLeftButtonUp += (_, _) => viewModel.NavigateInto(cell.Row!);
            }

            Canvas.SetLeft(border, rect.X + gap / 2);
            Canvas.SetTop(border, rect.Y + gap / 2);
            TreemapCanvas.Children.Add(border);
        }
    }

    private static Color LerpColor(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromRgb(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }

    private static double RelativeLuminance(Color color) =>
        (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
}

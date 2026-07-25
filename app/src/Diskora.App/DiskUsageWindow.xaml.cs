using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Diskora.App.ViewModels;
using Diskora.Core.Services;

namespace Diskora.App;

public partial class DiskUsageWindow : Window
{
    public DiskUsageWindow(string rootPath)
    {
        InitializeComponent();
        var viewModel = new DiskUsageViewModel(new DiskUsageScanner(), rootPath);
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

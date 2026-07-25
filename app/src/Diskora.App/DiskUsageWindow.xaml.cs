using System.Windows;
using System.Windows.Input;
using Diskora.App.ViewModels;
using Diskora.Core.Services;

namespace Diskora.App;

public partial class DiskUsageWindow : Window
{
    public DiskUsageWindow(string rootPath)
    {
        InitializeComponent();
        DataContext = new DiskUsageViewModel(new DiskUsageScanner(), rootPath);
    }

    private void ItemsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemsGrid.SelectedItem is DiskUsageNodeRowViewModel row && DataContext is DiskUsageViewModel viewModel)
        {
            viewModel.NavigateInto(row);
        }
    }
}

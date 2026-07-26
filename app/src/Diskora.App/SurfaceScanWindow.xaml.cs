using System.Windows;
using Diskora.App.ViewModels;
using Diskora.Core.Services;

namespace Diskora.App;

public partial class SurfaceScanWindow : Window
{
    public SurfaceScanWindow(int physicalDiskIndex, string diskName, long sizeBytes)
    {
        InitializeComponent();
        DataContext = new SurfaceScanViewModel(new SurfaceScanService(), physicalDiskIndex, diskName, sizeBytes);
    }
}

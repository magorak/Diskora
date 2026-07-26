using System.Windows;
using Diskora.App.ViewModels;
using Diskora.Core.Services;

namespace Diskora.App;

public partial class OptimizationWindow : Window
{
    public OptimizationWindow(string driveLetter, string volumeName)
    {
        InitializeComponent();
        DataContext = new OptimizationViewModel(
            new DiskOptimizationService(), new FragmentationAnalysisService(), driveLetter, volumeName);
    }
}

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

    /// <summary>
    /// Analýza vypisuje výsledky na jinou záložku, než na které uživatel stojí,
    /// takže bez přepnutí nebylo poznat, jestli se vůbec něco děje (nahlásil uživatel).
    /// </summary>
    private void AnalyzeFragmentation_Click(object sender, RoutedEventArgs e) =>
        Tabs.SelectedItem = FragmentationTab;
}

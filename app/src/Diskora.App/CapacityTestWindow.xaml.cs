using System.Windows;
using Diskora.App.ViewModels;
using Diskora.Core.Services;

namespace Diskora.App;

public partial class CapacityTestWindow : Window
{
    public CapacityTestWindow(string driveLetter, string volumeName)
    {
        InitializeComponent();
        DataContext = new CapacityTestViewModel(new CapacityTestService(), driveLetter, volumeName);
    }
}

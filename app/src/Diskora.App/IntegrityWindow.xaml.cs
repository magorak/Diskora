using System.Windows;
using Diskora.App.ViewModels;
using Diskora.Core.Services;

namespace Diskora.App;

public partial class IntegrityWindow : Window
{
    public IntegrityWindow(string driveLetter, string volumeName)
    {
        InitializeComponent();
        DataContext = new IntegrityViewModel(new IntegrityCheckService(), driveLetter, volumeName);
    }
}

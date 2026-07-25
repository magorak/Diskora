using System.Windows;
using Diskora.App.ViewModels;
using Diskora.Core.Services;

namespace Diskora.App;

public partial class SystemEventLogWindow : Window
{
    public SystemEventLogWindow()
    {
        InitializeComponent();
        DataContext = new SystemEventLogViewModel(new DiskEventLogService());
    }
}

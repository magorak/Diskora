using System.Windows;
using Diskora.App.ViewModels;
using Diskora.Core.Services;
using Diskora.Data;

namespace Diskora.App;

public partial class IntegrityWindow : Window
{
    public IntegrityWindow(string driveLetter, string volumeName)
    {
        InitializeComponent();
        var historyStore = new SqliteDiskHistoryStore();
        DataContext = new IntegrityViewModel(new IntegrityCheckService(historyStore), historyStore, driveLetter, volumeName);
    }
}

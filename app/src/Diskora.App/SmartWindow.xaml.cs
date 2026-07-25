using System.Windows;
using Diskora.App.ViewModels;
using Diskora.Core.Services;
using Diskora.Data;

namespace Diskora.App;

public partial class SmartWindow : Window
{
    public SmartWindow(int diskIndex, string diskName)
    {
        InitializeComponent();
        var historyStore = new SqliteDiskHistoryStore();
        DataContext = new SmartViewModel(new SmartService(historyStore), historyStore, diskIndex, diskName);
    }
}

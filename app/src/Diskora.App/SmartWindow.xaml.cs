using System.Windows;
using Diskora.App.ViewModels;
using Diskora.Core.Services;

namespace Diskora.App;

public partial class SmartWindow : Window
{
    public SmartWindow(int diskIndex, string diskName)
    {
        InitializeComponent();
        DataContext = new SmartViewModel(new SmartService(), diskIndex, diskName);
    }
}

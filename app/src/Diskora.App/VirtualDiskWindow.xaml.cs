using System.Windows;
using Diskora.App.ViewModels;
using Diskora.Core.Services;
using Diskora.Data;

namespace Diskora.App;

public partial class VirtualDiskWindow : Window
{
    public VirtualDiskWindow(string path)
    {
        InitializeComponent();
        DataContext = new VirtualDiskViewModel(new VirtualDiskService(new SqliteVirtualDiskAttachmentRegistry()), path);
    }
}

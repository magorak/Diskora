using System.Windows;
using Diskora.App.Theming;

namespace Diskora.App;

public partial class App : Application
{
    public ThemeService Theme { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Theme = new ThemeService(this);
        Theme.Apply(AppTheme.System);
    }
}

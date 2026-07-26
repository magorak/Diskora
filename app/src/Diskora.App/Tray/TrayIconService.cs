using System.Windows;

namespace Diskora.App.Tray;

/// <summary>
/// Ikona v oznamovací oblasti (system tray) přes <see cref="System.Windows.Forms.NotifyIcon"/>
/// - WPF vlastní tray API nemá, jde o standardní interop (<c>UseWindowsForms</c> v csproj),
/// bez další externí závislosti. Typy z <c>System.Windows.Forms</c> jsou plně kvalifikované
/// místo <c>using</c>, protože obě jmenné prostory (WPF i WinForms) definují vlastní
/// <c>Application</c> - <c>using</c> obojího by kolidovalo.
/// Umožňuje Diskoru minimalizovat "na pozadí" a připravuje půdu pro balónková
/// upozornění na zhoršení zdraví disku (Fáze 2), aniž by musela zůstávat viditelná.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _notifyIcon;

    public TrayIconService(Window mainWindow)
    {
        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "Diskora",
            Visible = false,
        };

        var contextMenu = new System.Windows.Forms.ContextMenuStrip();
        contextMenu.Items.Add("Zobrazit Diskoru", null, (_, _) => Restore(mainWindow));
        contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        contextMenu.Items.Add("Konec", null, (_, _) => Application.Current.Shutdown());
        _notifyIcon.ContextMenuStrip = contextMenu;

        _notifyIcon.DoubleClick += (_, _) => Restore(mainWindow);
    }

    public void Show() => _notifyIcon.Visible = true;

    public void Hide() => _notifyIcon.Visible = false;

    public void ShowBalloonTip(string title, string text) =>
        _notifyIcon.ShowBalloonTip(5000, title, text, System.Windows.Forms.ToolTipIcon.Info);

    private static void Restore(Window window)
    {
        window.Show();
        window.WindowState = WindowState.Normal;
        window.Activate();
    }

    private static System.Drawing.Icon LoadIcon()
    {
        var uri = new Uri("pack://application:,,,/Assets/AppIcon.ico", UriKind.Absolute);
        var streamInfo = Application.GetResourceStream(uri)
            ?? throw new InvalidOperationException("Ikona aplikace (Assets/AppIcon.ico) nebyla v assembly nalezena.");
        using var stream = streamInfo.Stream;
        return new System.Drawing.Icon(stream);
    }

    public void Dispose()
    {
        _notifyIcon.Dispose();
    }
}

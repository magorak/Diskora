using System.IO;
using System.Windows.Threading;
using Diskora.Core.Models;
using Diskora.Core.Services;

namespace Diskora.App.Tray;

/// <summary>
/// Periodicky (na pozadí, mimo UI vlákno) kontroluje zhoršení zdraví disků a
/// zobrazí balónkové upozornění přes <see cref="TrayIconService"/> - jde
/// jen o tenké propojení, veškerá rozhodovací logika je v testovaném
/// <see cref="IDiskHealthMonitor"/>. Bez SMART (chybí admin práva, USB most,
/// nepodporovaný řadič) se disk tiše přeskočí - to řeší už monitor sám.
/// </summary>
public sealed class DiskHealthNotifier : IDisposable
{
    private readonly IDiskEnumerationService _diskEnumerationService;
    private readonly IDiskHealthMonitor _healthMonitor;
    private readonly TrayIconService _trayIcon;
    private readonly DispatcherTimer _timer;

    public DiskHealthNotifier(
        IDiskEnumerationService diskEnumerationService,
        IDiskHealthMonitor healthMonitor,
        TrayIconService trayIcon,
        TimeSpan interval)
    {
        _diskEnumerationService = diskEnumerationService;
        _healthMonitor = healthMonitor;
        _trayIcon = trayIcon;

        _timer = new DispatcherTimer { Interval = interval };
        _timer.Tick += async (_, _) => await CheckNowAsync();
        _timer.Start();
    }

    /// <summary>
    /// Veřejné, ať jde vyvolat okamžitě (např. krátce po startu), ne jen čekat na první
    /// tik časovače. Samotné čtení (WMI enumerace + SMART I/O) běží na vlákně z fondu
    /// přes <see cref="Task.Run(Action)"/>, ať neblokuje UI vlákno - `await` se pak
    /// vrátí zpátky na UI vlákno (díky WPF `SynchronizationContext`), takže volání
    /// <see cref="TrayIconService.ShowBalloonTip"/> níže je bezpečné bez dalšího marshaling.
    /// </summary>
    public async Task CheckNowAsync()
    {
        IReadOnlyList<DiskHealthChangeResult> degraded;
        try
        {
            degraded = await Task.Run(() =>
            {
                var diskIndexes = _diskEnumerationService.GetPhysicalDisks().Select(d => d.Index).ToList();
                return _healthMonitor.CheckForDegradation(diskIndexes);
            });
        }
        catch (Exception ex) when (ex is System.Management.ManagementException or UnauthorizedAccessException or IOException)
        {
            return;
        }

        foreach (var change in degraded)
        {
            _trayIcon.ShowBalloonTip(
                "Zhoršení zdraví disku",
                $"Disk {change.DiskIndex}: {Describe(change.PreviousHealth)} → {Describe(change.CurrentHealth)}. " +
                "Otevřete Diskoru a zkontrolujte S.M.A.R.T. atributy.");
        }
    }

    private static string Describe(DiskHealthStatus status) => status switch
    {
        DiskHealthStatus.Healthy => "v pořádku",
        DiskHealthStatus.Warning => "varování",
        DiskHealthStatus.Critical => "kritické",
        _ => "neznámé",
    };

    public void Dispose()
    {
        _timer.Stop();
    }
}

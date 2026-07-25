using Diskora.App.Display;
using Diskora.Core.Models;

namespace Diskora.App.ViewModels;

public sealed class SmartHistoryRowViewModel(SmartHistoryEntry entry)
{
    public string TimestampDisplay { get; } = entry.RecordedAtUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss");

    public DiskHealthStatus Health { get; } = entry.OverallHealth;

    public string HealthDisplay { get; } = entry.OverallHealth.ToDisplayText();
}

using Diskora.App.Display;
using Diskora.Core.Models;

namespace Diskora.App.ViewModels;

public sealed class DiskEventLogRowViewModel(DiskEventLogEntry entry)
{
    public string TimeDisplay { get; } = entry.TimeCreated.ToString("dd.MM.yyyy HH:mm:ss");

    public DiskEventLevel Level { get; } = entry.Level;

    public string LevelDisplay { get; } = entry.Level.ToDisplayText();

    public string LogName { get; } = entry.LogName;

    public string ProviderName { get; } = entry.ProviderName;

    public int EventId { get; } = entry.EventId;

    public string Message { get; } = entry.Message;
}

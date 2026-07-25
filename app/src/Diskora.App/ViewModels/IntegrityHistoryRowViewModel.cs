using Diskora.App.Display;
using Diskora.Core.Models;

namespace Diskora.App.ViewModels;

public sealed class IntegrityHistoryRowViewModel(IntegrityHistoryEntry entry)
{
    public string TimestampDisplay { get; } = entry.RecordedAtUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss");

    public VolumeDirtyState DirtyState { get; } = entry.DirtyState;

    public string DirtyStateDisplay { get; } = entry.DirtyState.ToDisplayText();

    public string ScanDisplay { get; } = entry.ScanExitCode is null
        ? "—"
        : entry.ScanAppearsClean == true
            ? $"Sken OK (kód {entry.ScanExitCode})"
            : $"Sken: kód {entry.ScanExitCode}";
}

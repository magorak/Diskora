namespace Diskora.Core.Models;

public sealed record IntegrityHistoryEntry(
    long Id,
    string DriveLetter,
    DateTimeOffset RecordedAtUtc,
    VolumeDirtyState DirtyState,
    int? ScanExitCode,
    bool? ScanAppearsClean);

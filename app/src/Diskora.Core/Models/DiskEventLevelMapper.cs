namespace Diskora.Core.Models;

/// <summary>
/// Maps the raw <c>EventRecord.Level</c> byte (per winmeta.xml: 1=Critical, 2=Error,
/// 3=Warning, 4=Information) to <see cref="DiskEventLevel"/>. Kept separate from
/// Diskora.Native so the mapping is unit-testable without touching the real event log.
/// </summary>
public static class DiskEventLevelMapper
{
    public static DiskEventLevel FromRawLevel(byte? level) => level switch
    {
        1 => DiskEventLevel.Critical,
        2 => DiskEventLevel.Error,
        3 => DiskEventLevel.Warning,
        4 => DiskEventLevel.Information,
        _ => DiskEventLevel.Unknown,
    };
}

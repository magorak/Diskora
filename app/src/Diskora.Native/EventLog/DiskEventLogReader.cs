using System.Diagnostics.Eventing.Reader;

namespace Diskora.Native.EventLog;

/// <summary>
/// Reads recent disk/filesystem-related entries from the Windows Event Log
/// (System log providers such as Ntfs, Disk, Volsnap, Virtual Disk Service,
/// FilterManager, and Wininit's autochk results). Read-only, no elevation
/// required for the standard System/Application logs.
/// </summary>
public static class DiskEventLogReader
{
    private static readonly string[] RelevantProviders =
    [
        "Microsoft-Windows-Ntfs",
        "Disk",
        "Volsnap",
        "Virtual Disk Service",
        "Microsoft-Windows-FilterManager",
        "Microsoft-Windows-Wininit",
        "Microsoft-Windows-Storage-ClassPnP",
    ];

    public static IReadOnlyList<NativeDiskEventLogEntry> GetRecentDiskEvents(int maxEntries = 50)
    {
        var entries = new List<NativeDiskEventLogEntry>();

        foreach (var logName in new[] { "System", "Application" })
        {
            entries.AddRange(ReadLog(logName, maxEntries));
        }

        return entries
            .OrderByDescending(e => e.TimeCreated)
            .Take(maxEntries)
            .ToList();
    }

    private static IEnumerable<NativeDiskEventLogEntry> ReadLog(string logName, int maxEntries)
    {
        var providerCondition = string.Join(" or ", RelevantProviders.Select(p => $"Provider[@Name='{p}']"));
        var xpath = $"*[System[({providerCondition})]]";

        EventLogReader reader;
        try
        {
            var query = new EventLogQuery(logName, PathType.LogName, xpath) { ReverseDirection = true };
            reader = new EventLogReader(query);
        }
        catch (EventLogNotFoundException)
        {
            yield break;
        }

        using (reader)
        {
            var count = 0;
            while (count < maxEntries && reader.ReadEvent() is { } record)
            {
                using (record)
                {
                    string message;
                    try
                    {
                        message = record.FormatDescription() ?? "(bez popisu)";
                    }
                    catch (EventLogException)
                    {
                        message = "(popis události není k dispozici - chybí zdrojová šablona)";
                    }

                    yield return new NativeDiskEventLogEntry(
                        record.TimeCreated ?? DateTime.MinValue,
                        record.Level,
                        logName,
                        record.ProviderName,
                        record.Id,
                        message);
                }

                count++;
            }
        }
    }
}

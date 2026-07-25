namespace Diskora.Native.EventLog;

public sealed record NativeDiskEventLogEntry(
    DateTime TimeCreated,
    byte? RawLevel,
    string LogName,
    string ProviderName,
    int EventId,
    string Message);

namespace Diskora.Core.Models;

public sealed record DiskEventLogEntry(
    DateTime TimeCreated,
    DiskEventLevel Level,
    string LogName,
    string ProviderName,
    int EventId,
    string Message);

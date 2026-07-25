namespace Diskora.Core.Models;

public sealed record PhysicalDiskInfo(
    int Index,
    string FriendlyName,
    ulong SizeBytes,
    DiskMediaType MediaType,
    DiskBusType BusType,
    string? SerialNumber);

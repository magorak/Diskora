namespace Diskora.Core.Models;

public sealed record BadSectorRange(long OffsetBytes, long LengthBytes);

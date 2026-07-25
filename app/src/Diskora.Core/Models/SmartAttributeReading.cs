namespace Diskora.Core.Models;

/// <summary>Syrová hodnota jednoho S.M.A.R.T. atributu přečtená z disku.</summary>
public sealed record SmartAttributeReading(
    byte Id,
    byte CurrentValue,
    byte WorstValue,
    byte Threshold,
    ulong RawValue);

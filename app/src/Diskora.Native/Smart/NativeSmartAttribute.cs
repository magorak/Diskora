namespace Diskora.Native.Smart;

/// <summary>Syrová hodnota jednoho ATA S.M.A.R.T. atributu, jak přišla z IOCTL.</summary>
public readonly record struct NativeSmartAttribute(byte Id, byte CurrentValue, byte WorstValue, byte Threshold, ulong RawValue);

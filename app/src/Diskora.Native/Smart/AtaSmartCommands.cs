namespace Diskora.Native.Smart;

/// <summary>
/// Hodnoty ATA registrů pro rodinu příkazů SMART. Obě cesty k datům (legacy
/// IOCTL i ATA pass-through) posílají disku úplně stejný příkaz, jen jinou
/// obálkou - konstanty jsou proto společné.
/// </summary>
internal static class AtaSmartCommands
{
    /// <summary>Příkazový registr rodiny SMART.</summary>
    public const byte SmartCommand = 0xB0;

    /// <summary>Features: vrať tabulku atributů.</summary>
    public const byte ReadAttributeValues = 0xD0;

    /// <summary>Features: vrať prahy selhání. V novějších revizích ATA zastaralé, disk to smí odmítnout.</summary>
    public const byte ReadThresholds = 0xD1;

    /// <summary>Podpis, kterým se příkaz identifikuje jako SMART (jinak by disk chápal LBA jako adresu).</summary>
    public const byte CylinderLow = 0x4F;

    public const byte CylinderHigh = 0xC2;

    public const byte DeviceHead = 0xA0;
}

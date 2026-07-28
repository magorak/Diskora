namespace Diskora.Core.Diagnostics;

/// <summary>
/// Vzor pro test skutečné kapacity: pro každý bajt na disku umí spočítat, jaká
/// hodnota tam má být, jen z jeho pozice. Díky tomu se nemusí nic ukládat
/// stranou - ověření si hodnotu spočítá znovu a porovná.
///
/// Proč ne prostě nuly nebo opakující se řetězec: přeznačené flash disky obvykle
/// adresy nad skutečnou kapacitou „zabalí" zpátky na začátek, takže zápis přepíše
/// dřívější data. Se vzorem závislým na pozici se to pozná okamžitě - na
/// přečteném místě je hodnota patřící jiné adrese. U konstantního vzoru by
/// takový disk prošel jako zdravý.
/// </summary>
public static class CapacityTestPattern
{
    /// <summary>Naplní buffer hodnotami, které patří pozicím počínaje <paramref name="absoluteOffset"/>.</summary>
    public static void Fill(Span<byte> buffer, long absoluteOffset)
    {
        for (var i = 0; i < buffer.Length; i++)
        {
            buffer[i] = ByteAt(absoluteOffset + i);
        }
    }

    /// <summary>
    /// Absolutní pozice prvního bajtu, který nesedí, nebo null když je celý blok
    /// v pořádku.
    /// </summary>
    public static long? FindFirstMismatch(ReadOnlySpan<byte> buffer, long absoluteOffset)
    {
        for (var i = 0; i < buffer.Length; i++)
        {
            if (buffer[i] != ByteAt(absoluteOffset + i))
            {
                return absoluteOffset + i;
            }
        }

        return null;
    }

    /// <summary>Hodnota, která patří na danou absolutní pozici.</summary>
    public static byte ByteAt(long offset)
    {
        // Osmice bajtů sdílí jedno promíchané číslo; pozice uvnitř osmice vybere bajt.
        var mixed = Mix((ulong)(offset >> 3));
        return (byte)(mixed >> (int)((offset & 7) * 8));
    }

    /// <summary>
    /// SplitMix64 - levné, ale dobře promíchá i sousední hodnoty, takže se dva
    /// blízké offsety neliší jen v jednom bitu (to by u posunutých dat mohlo projít).
    /// </summary>
    private static ulong Mix(ulong value)
    {
        value += 0x9E3779B97F4A7C15UL;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}

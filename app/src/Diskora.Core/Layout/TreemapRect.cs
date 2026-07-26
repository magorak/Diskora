namespace Diskora.Core.Layout;

/// <summary>Jeden obdélník vypočtený <see cref="SquarifiedTreemapLayout"/> - čisté geometrické
/// souřadnice, bez závislosti na WPF, ať jde vykreslit v libovolném UI frameworku.</summary>
public readonly record struct TreemapRect(double X, double Y, double Width, double Height);

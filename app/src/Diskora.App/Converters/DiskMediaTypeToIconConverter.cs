using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Diskora.Core.Models;

namespace Diskora.App.Converters;

/// <summary>
/// Ikona typu disku jako vektorová cesta. Kreslené, ne obrázkové - škálují se
/// s DPI, dědí barvu z okolí a nepřidávají do sestavení žádný soubor navíc.
///
/// Tvary jsou záměrně jednoduché a rozlišitelné na první pohled i v malé
/// velikosti: talířový disk kolečkem (ploténka), SSD obdélníkem s čipem,
/// vyměnitelný disk konektorem. Symbol musí nést informaci sám o sobě, ne jen
/// barvou - kvůli barvosleposti a černobílému tisku zprávy.
/// </summary>
public sealed class DiskMediaTypeToIconConverter : IValueConverter
{
    // Souřadnice v mřížce 24×24, aby se daly kombinovat s jinými ikonami.
    private const string HardDisk =
        "M12 4a8 8 0 1 0 0 16 8 8 0 0 0 0-16zm0 5.5a2.5 2.5 0 1 1 0 5 2.5 2.5 0 0 1 0-5zm4.9 8.6 -3.1-3.1";
    private const string SolidState =
        "M4 6h16v12H4zM7 9h10v6H7zM9 18v2M15 18v2M9 4v2M15 4v2";
    private const string Removable =
        "M7 3h10v11l-5 7-5-7zM10 6h4";
    private const string Virtual =
        "M12 4c4.4 0 8 1.3 8 3v10c0 1.7-3.6 3-8 3s-8-1.3-8-3V7c0-1.7 3.6-3 8-3zM4 7c0 1.7 3.6 3 8 3s8-1.3 8-3";
    private const string Unknown =
        "M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18zm0 13.5v.01M12 8a2 2 0 0 1 1.4 3.4c-.6.6-1.4.9-1.4 2.1";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Geometry.Parse(value switch
        {
            DiskMediaType.HardDisk => HardDisk,
            DiskMediaType.SolidState or DiskMediaType.StorageClassMemory => SolidState,
            DiskMediaType.Removable => Removable,
            DiskMediaType.Virtual => Virtual,
            _ => Unknown,
        });

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

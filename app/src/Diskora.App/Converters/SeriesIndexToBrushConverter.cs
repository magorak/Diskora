using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Diskora.App.Converters;

/// <summary>
/// Mapuje index série (0-4) na pevně danou barvu z kategoriální palety
/// (skill dataviz - pořadí je bezpečnostní mechanismus, ne kosmetika, proto
/// se necykluje). Cokoli mimo 0-4 (typicky -1 pro souhrnnou položku "Ostatní")
/// dostane tlumenou barvu popředí, ne další vygenerovaný odstín.
/// </summary>
public sealed class SeriesIndexToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value is int index and >= 0 and <= 4 ? $"SeriesBrush{index + 1}" : "MutedForegroundBrush";
        return Application.Current.TryFindResource(key) ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Diskora.App.Converters;

/// <summary>
/// True = úspěch (zelená), false = problém (červená). Pro jednoduchá „prošlo /
/// neprošlo" shrnutí, kde by tříhodnotový RiskToBrushConverter byl zbytečný.
/// </summary>
public sealed class BooleanToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Application.Current.TryFindResource(value is true ? "SuccessBrush" : "DangerBrush") ?? Brushes.Gray;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

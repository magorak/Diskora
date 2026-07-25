using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Diskora.Core.Models;

namespace Diskora.App.Converters;

/// <summary>Barevné odznaky typu disku v dashboardu (SSD/HDD/vyměnitelný/virtuální).</summary>
public sealed class DiskMediaTypeToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            DiskMediaType.SolidState => "SuccessBrush",
            DiskMediaType.StorageClassMemory => "SuccessBrush",
            DiskMediaType.HardDisk => "AccentBrush",
            DiskMediaType.Removable => "WarningBrush",
            _ => "MutedForegroundBrush",
        };

        return Application.Current.TryFindResource(key) ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Diskora.Core.Models;

namespace Diskora.App.Converters;

public sealed class RiskToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var resourceKey = value switch
        {
            DiskHealthStatus.Healthy => "SuccessBrush",
            DiskHealthStatus.Warning => "WarningBrush",
            DiskHealthStatus.Critical => "DangerBrush",
            SmartAttributeRisk.Ok => "SuccessBrush",
            SmartAttributeRisk.Warning => "WarningBrush",
            SmartAttributeRisk.Critical => "DangerBrush",
            VolumeDirtyState.Clean => "SuccessBrush",
            VolumeDirtyState.Dirty => "DangerBrush",
            DiskEventLevel.Information => "MutedForegroundBrush",
            DiskEventLevel.Warning => "WarningBrush",
            DiskEventLevel.Error => "DangerBrush",
            DiskEventLevel.Critical => "DangerBrush",
            _ => "MutedForegroundBrush",
        };

        return Application.Current.TryFindResource(resourceKey) ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

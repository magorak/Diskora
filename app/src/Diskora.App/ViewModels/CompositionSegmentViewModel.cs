namespace Diskora.App.ViewModels;

/// <summary>Jedna položka kompozičního pruhu zaplněnosti (viz DiskUsageViewModel).</summary>
public sealed class CompositionSegmentViewModel
{
    public required string Name { get; init; }

    public required double Percent { get; init; }

    public required string SizeDisplay { get; init; }

    /// <summary>0-4 pro pojmenované položky, -1 pro souhrnnou položku "Ostatní".</summary>
    public required int SeriesIndex { get; init; }

    public string PercentDisplay => $"{Percent:F1} %";

    public string TooltipText => $"{Name}: {SizeDisplay} ({PercentDisplay})";
}

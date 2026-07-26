namespace Diskora.App.ViewModels;

/// <summary>
/// Jedna buňka treemapy zaplněnosti aktuální složky - buď reálná podsložka (klikatelná,
/// drill-down přes <see cref="Row"/>), nebo souhrnná položka za přímé soubory v této
/// složce (<see cref="Row"/> je null, nedá se do ní vstoupit).
/// </summary>
public sealed class TreemapCellViewModel
{
    public required string Name { get; init; }

    public required long SizeBytes { get; init; }

    public required string SizeDisplay { get; init; }

    public required double PercentOfParent { get; init; }

    public DiskUsageNodeRowViewModel? Row { get; init; }

    public bool IsNavigable => Row?.CanNavigateInto == true;

    public string TooltipText => $"{Name}: {SizeDisplay} ({PercentOfParent:F1} %)";
}

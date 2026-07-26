using Diskora.Core.Models;

namespace Diskora.Core.Services;

public sealed class DiskHealthMonitor(ISmartService smartService, IDiskHistoryStore historyStore) : IDiskHealthMonitor
{
    public IReadOnlyList<DiskHealthChangeResult> CheckForDegradation(IReadOnlyList<int> diskIndexes)
    {
        var results = new List<DiskHealthChangeResult>();

        foreach (var diskIndex in diskIndexes)
        {
            // Musí se přečíst PŘED voláním ReadReport - to samo zapíše nový řádek historie
            // s AKTUÁLNÍM zdravím, takže by se jinak srovnávalo čerstvé čtení samo se sebou.
            var previousEntries = historyStore.GetRecentSmartHistory(diskIndex, maxCount: 1);
            var previous = previousEntries.Count > 0 ? previousEntries[0].OverallHealth : (DiskHealthStatus?)null;

            var outcome = smartService.ReadReport(diskIndex);
            if (!outcome.IsSupported || outcome.Report is null)
            {
                continue;
            }

            var current = outcome.Report.OverallHealth;
            if (DiskHealthChangeDetector.HasDegraded(previous, current))
            {
                results.Add(new DiskHealthChangeResult(diskIndex, previous!.Value, current));
            }
        }

        return results;
    }
}

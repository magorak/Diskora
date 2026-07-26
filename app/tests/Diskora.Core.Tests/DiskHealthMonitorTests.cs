using Diskora.Core.Models;
using Diskora.Core.Services;

namespace Diskora.Core.Tests;

public sealed class DiskHealthMonitorTests
{
    private sealed class FakeSmartService : ISmartService
    {
        public Dictionary<int, SmartReadResult> Results { get; } = [];

        public SmartReadResult ReadReport(int physicalDiskIndex) => Results[physicalDiskIndex];
    }

    private sealed class FakeHistoryStore : IDiskHistoryStore
    {
        public Dictionary<int, DiskHealthStatus> PreviousHealth { get; } = [];

        public void RecordSmartReading(int diskIndex, DiskHealthStatus overallHealth)
        {
        }

        public IReadOnlyList<SmartHistoryEntry> GetRecentSmartHistory(int diskIndex, int maxCount = 20) =>
            PreviousHealth.TryGetValue(diskIndex, out var health)
                ? [new SmartHistoryEntry(1, diskIndex, DateTimeOffset.UtcNow, health)]
                : [];

        public void RecordIntegrityCheck(string driveLetter, VolumeDirtyState dirtyState, int? scanExitCode, bool? scanAppearsClean)
        {
        }

        public IReadOnlyList<IntegrityHistoryEntry> GetRecentIntegrityHistory(string driveLetter, int maxCount = 20) => [];
    }

    private static SmartReadResult SupportedResult(int diskIndex, DiskHealthStatus health) =>
        new(true, null, new SmartReport(diskIndex, DateTimeOffset.UtcNow, [], health));

    private static readonly SmartReadResult UnsupportedResult = new(false, "SMART není dostupné", null);

    [Fact]
    public void CheckForDegradation_HealthWorsened_ReturnsResult()
    {
        var smartService = new FakeSmartService();
        var historyStore = new FakeHistoryStore();
        historyStore.PreviousHealth[0] = DiskHealthStatus.Healthy;
        smartService.Results[0] = SupportedResult(0, DiskHealthStatus.Critical);

        var monitor = new DiskHealthMonitor(smartService, historyStore);
        var results = monitor.CheckForDegradation([0]);

        var result = Assert.Single(results);
        Assert.Equal(0, result.DiskIndex);
        Assert.Equal(DiskHealthStatus.Healthy, result.PreviousHealth);
        Assert.Equal(DiskHealthStatus.Critical, result.CurrentHealth);
    }

    [Fact]
    public void CheckForDegradation_HealthUnchanged_ReturnsEmpty()
    {
        var smartService = new FakeSmartService();
        var historyStore = new FakeHistoryStore();
        historyStore.PreviousHealth[0] = DiskHealthStatus.Warning;
        smartService.Results[0] = SupportedResult(0, DiskHealthStatus.Warning);

        var monitor = new DiskHealthMonitor(smartService, historyStore);

        Assert.Empty(monitor.CheckForDegradation([0]));
    }

    [Fact]
    public void CheckForDegradation_SmartUnsupported_SkipsDiskSilently()
    {
        var smartService = new FakeSmartService();
        var historyStore = new FakeHistoryStore();
        historyStore.PreviousHealth[0] = DiskHealthStatus.Healthy;
        smartService.Results[0] = UnsupportedResult;

        var monitor = new DiskHealthMonitor(smartService, historyStore);

        Assert.Empty(monitor.CheckForDegradation([0]));
    }

    [Fact]
    public void CheckForDegradation_NoHistoryYet_ReturnsEmpty()
    {
        var smartService = new FakeSmartService();
        var historyStore = new FakeHistoryStore();
        smartService.Results[0] = SupportedResult(0, DiskHealthStatus.Critical);

        var monitor = new DiskHealthMonitor(smartService, historyStore);

        Assert.Empty(monitor.CheckForDegradation([0]));
    }

    [Fact]
    public void CheckForDegradation_MultipleDisks_OnlyReportsDegradedOnes()
    {
        var smartService = new FakeSmartService();
        var historyStore = new FakeHistoryStore();
        historyStore.PreviousHealth[0] = DiskHealthStatus.Healthy;
        historyStore.PreviousHealth[1] = DiskHealthStatus.Healthy;
        smartService.Results[0] = SupportedResult(0, DiskHealthStatus.Warning);
        smartService.Results[1] = SupportedResult(1, DiskHealthStatus.Healthy);

        var monitor = new DiskHealthMonitor(smartService, historyStore);
        var results = monitor.CheckForDegradation([0, 1]);

        var result = Assert.Single(results);
        Assert.Equal(0, result.DiskIndex);
    }
}

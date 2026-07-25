using Diskora.Core.Models;

namespace Diskora.Data.Tests;

public sealed class SqliteDiskHistoryStoreTests : IDisposable
{
    private readonly string _databasePath;
    private readonly SqliteDiskHistoryStore _store;

    public SqliteDiskHistoryStoreTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"diskora-tests-{Guid.NewGuid():N}.db");
        _store = new SqliteDiskHistoryStore(_databasePath);
    }

    public void Dispose()
    {
        // SQLite drží soubor otevřený přes connection pooling - explicitně ho uvolníme,
        // ať smazání dočasného souboru níže nespadne na "soubor je používán".
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try
        {
            File.Delete(_databasePath);
        }
        catch (IOException)
        {
            // Best effort cleanup.
        }
    }

    [Fact]
    public void RecordSmartReading_ThenGetRecent_ReturnsEntryNewestFirst()
    {
        _store.RecordSmartReading(0, DiskHealthStatus.Healthy);
        _store.RecordSmartReading(0, DiskHealthStatus.Warning);

        var history = _store.GetRecentSmartHistory(0);

        Assert.Equal(2, history.Count);
        Assert.Equal(DiskHealthStatus.Warning, history[0].OverallHealth);
        Assert.Equal(DiskHealthStatus.Healthy, history[1].OverallHealth);
    }

    [Fact]
    public void GetRecentSmartHistory_DifferentDiskIndex_IsIsolated()
    {
        _store.RecordSmartReading(0, DiskHealthStatus.Healthy);
        _store.RecordSmartReading(1, DiskHealthStatus.Critical);

        var disk0History = _store.GetRecentSmartHistory(0);
        var disk1History = _store.GetRecentSmartHistory(1);

        Assert.Single(disk0History);
        Assert.Single(disk1History);
        Assert.Equal(DiskHealthStatus.Healthy, disk0History[0].OverallHealth);
        Assert.Equal(DiskHealthStatus.Critical, disk1History[0].OverallHealth);
    }

    [Fact]
    public void GetRecentSmartHistory_RespectsMaxCount()
    {
        for (var i = 0; i < 5; i++)
        {
            _store.RecordSmartReading(0, DiskHealthStatus.Healthy);
        }

        var history = _store.GetRecentSmartHistory(0, maxCount: 3);

        Assert.Equal(3, history.Count);
    }

    [Fact]
    public void GetRecentSmartHistory_NoEntries_ReturnsEmpty()
    {
        var history = _store.GetRecentSmartHistory(42);

        Assert.Empty(history);
    }

    [Fact]
    public void RecordIntegrityCheck_ThenGetRecent_RoundTripsAllFields()
    {
        _store.RecordIntegrityCheck("C:\\", VolumeDirtyState.Clean, scanExitCode: 0, scanAppearsClean: true);

        var history = _store.GetRecentIntegrityHistory("C:\\");

        var entry = Assert.Single(history);
        Assert.Equal(VolumeDirtyState.Clean, entry.DirtyState);
        Assert.Equal(0, entry.ScanExitCode);
        Assert.True(entry.ScanAppearsClean);
    }

    [Fact]
    public void RecordIntegrityCheck_WithoutScan_LeavesScanFieldsNull()
    {
        _store.RecordIntegrityCheck("D:\\", VolumeDirtyState.Unknown, scanExitCode: null, scanAppearsClean: null);

        var entry = Assert.Single(_store.GetRecentIntegrityHistory("D:\\"));

        Assert.Null(entry.ScanExitCode);
        Assert.Null(entry.ScanAppearsClean);
    }

    [Theory]
    [InlineData("E:\\", "e")]
    [InlineData("E:", "e:\\")]
    [InlineData("e", "E:")]
    public void DriveLetterLookup_IsCaseAndFormatInsensitive(string recordedAs, string queriedAs)
    {
        _store.RecordIntegrityCheck(recordedAs, VolumeDirtyState.Clean, null, null);

        var history = _store.GetRecentIntegrityHistory(queriedAs);

        Assert.Single(history);
    }

    [Fact]
    public void NewStoreInstance_ReusesExistingDatabaseFile()
    {
        _store.RecordSmartReading(0, DiskHealthStatus.Healthy);

        var reopened = new SqliteDiskHistoryStore(_databasePath);
        var history = reopened.GetRecentSmartHistory(0);

        Assert.Single(history);
    }
}

using Diskora.Core.Models;

namespace Diskora.Data.Tests;

public sealed class SqliteVirtualDiskAttachmentRegistryTests : IDisposable
{
    private readonly string _databasePath;
    private readonly string _trackedFilePath;
    private readonly SqliteVirtualDiskAttachmentRegistry _registry;

    public SqliteVirtualDiskAttachmentRegistryTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"diskora-tests-{Guid.NewGuid():N}.db");
        _trackedFilePath = Path.Combine(Path.GetTempPath(), $"diskora-tests-{Guid.NewGuid():N}.vhdx");
        File.WriteAllBytes(_trackedFilePath, []);
        _registry = new SqliteVirtualDiskAttachmentRegistry(_databasePath);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try
        {
            File.Delete(_databasePath);
        }
        catch (IOException)
        {
            // Best effort cleanup.
        }

        try
        {
            File.Delete(_trackedFilePath);
        }
        catch (IOException)
        {
            // Best effort cleanup.
        }
    }

    [Fact]
    public void RecordAttached_ThenGetTrackedAttachments_RoundTripsAllFields()
    {
        _registry.RecordAttached(_trackedFilePath, VirtualDiskFormat.Vhdx, readOnly: true);

        var entry = Assert.Single(_registry.GetTrackedAttachments());

        Assert.Equal(Path.GetFullPath(_trackedFilePath), entry.Path);
        Assert.Equal(VirtualDiskFormat.Vhdx, entry.Format);
        Assert.True(entry.ReadOnly);
    }

    [Fact]
    public void RecordAttached_SamePathTwice_DoesNotDuplicate()
    {
        _registry.RecordAttached(_trackedFilePath, VirtualDiskFormat.Vhd, readOnly: false);
        _registry.RecordAttached(_trackedFilePath, VirtualDiskFormat.Vhdx, readOnly: true);

        var entry = Assert.Single(_registry.GetTrackedAttachments());

        Assert.Equal(VirtualDiskFormat.Vhdx, entry.Format);
        Assert.True(entry.ReadOnly);
    }

    [Fact]
    public void RecordDetached_RemovesEntry()
    {
        _registry.RecordAttached(_trackedFilePath, VirtualDiskFormat.Iso, readOnly: true);

        _registry.RecordDetached(_trackedFilePath);

        Assert.Empty(_registry.GetTrackedAttachments());
    }

    [Fact]
    public void RecordDetached_IsCaseInsensitive()
    {
        _registry.RecordAttached(_trackedFilePath.ToUpperInvariant(), VirtualDiskFormat.Vhd, readOnly: false);

        _registry.RecordDetached(_trackedFilePath.ToLowerInvariant());

        Assert.Empty(_registry.GetTrackedAttachments());
    }

    [Fact]
    public void GetTrackedAttachments_FileNoLongerExists_PrunesEntrySilently()
    {
        _registry.RecordAttached(_trackedFilePath, VirtualDiskFormat.Vhd, readOnly: false);
        File.Delete(_trackedFilePath);

        var firstCall = _registry.GetTrackedAttachments();
        var secondCall = _registry.GetTrackedAttachments();

        Assert.Empty(firstCall);
        Assert.Empty(secondCall);
    }

    [Fact]
    public void GetTrackedAttachments_NoEntries_ReturnsEmpty()
    {
        Assert.Empty(_registry.GetTrackedAttachments());
    }

    [Fact]
    public void NewRegistryInstance_ReusesExistingDatabaseFile()
    {
        _registry.RecordAttached(_trackedFilePath, VirtualDiskFormat.Vhd, readOnly: false);

        var reopened = new SqliteVirtualDiskAttachmentRegistry(_databasePath);

        Assert.Single(reopened.GetTrackedAttachments());
    }
}

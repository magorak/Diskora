using System.IO;
using Diskora.App.Settings;

namespace Diskora.App.Tests;

public sealed class JsonAppSettingsStoreTests : IDisposable
{
    private readonly string _filePath;

    public JsonAppSettingsStoreTests()
    {
        _filePath = Path.Combine(Path.GetTempPath(), $"diskora-tests-{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        try
        {
            File.Delete(_filePath);
        }
        catch (IOException)
        {
            // Best effort cleanup.
        }
    }

    [Fact]
    public void Load_FileDoesNotExist_ReturnsDefaults()
    {
        var store = new JsonAppSettingsStore(_filePath);

        var settings = store.Load();

        Assert.Equal("System", settings.Theme);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsValues()
    {
        var store = new JsonAppSettingsStore(_filePath);

        store.Save(new AppSettings { Theme = "Dark" });
        var loaded = store.Load();

        Assert.Equal("Dark", loaded.Theme);
    }

    [Fact]
    public void Save_CreatesParentDirectoryIfMissing()
    {
        var nestedPath = Path.Combine(Path.GetTempPath(), $"diskora-tests-{Guid.NewGuid():N}", "settings.json");
        var store = new JsonAppSettingsStore(nestedPath);

        store.Save(new AppSettings { Theme = "Light" });

        Assert.True(File.Exists(nestedPath));
        Directory.Delete(Path.GetDirectoryName(nestedPath)!, recursive: true);
    }

    [Fact]
    public void Load_CorruptJson_ReturnsDefaultsInsteadOfThrowing()
    {
        File.WriteAllText(_filePath, "{ not valid json !!");
        var store = new JsonAppSettingsStore(_filePath);

        var settings = store.Load();

        Assert.Equal("System", settings.Theme);
    }

    [Fact]
    public void Load_FileDoesNotExist_LastSeenVersionIsEmpty()
    {
        // Prázdná hodnota = úplně první spuštění, na které App.ShowWhatsNewAfterUpdate
        // reaguje zobrazením okna „Co je nového".
        var settings = new JsonAppSettingsStore(_filePath).Load();

        Assert.Equal(string.Empty, settings.LastSeenVersion);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsLastSeenVersion()
    {
        var store = new JsonAppSettingsStore(_filePath);

        store.Save(new AppSettings { LastSeenVersion = "0.1.0" });

        Assert.Equal("0.1.0", new JsonAppSettingsStore(_filePath).Load().LastSeenVersion);
    }

    [Fact]
    public void Load_OlderFileWithoutNewFields_KeepsDefaults()
    {
        // Soubor uložený starší verzí Diskory nezná LastSeenVersion - nesmí to
        // shodit načtení ani přepsat ostatní volby.
        File.WriteAllText(_filePath, """{"Theme":"Dark"}""");

        var settings = new JsonAppSettingsStore(_filePath).Load();

        Assert.Equal("Dark", settings.Theme);
        Assert.Equal(string.Empty, settings.LastSeenVersion);
    }

    [Fact]
    public void NewStoreInstance_ReusesExistingFile()
    {
        var store = new JsonAppSettingsStore(_filePath);
        store.Save(new AppSettings { Theme = "Dark" });

        var reopened = new JsonAppSettingsStore(_filePath);

        Assert.Equal("Dark", reopened.Load().Theme);
    }
}

using System.IO;
using System.Text.Json;

namespace Diskora.App.Settings;

/// <summary>
/// Uživatelské předvolby jako obyčejný JSON soubor v %LocalAppData%\Diskora\settings.json -
/// na rozdíl od historie SMART/integrity (SQLite, `Diskora.Data`) jde jen o hrstku
/// skalárních hodnot bez potřeby dotazování, JSON soubor je tu prostší a je to
/// běžný vzor pro tenhle druh nastavení. Chybějící nebo poškozený soubor se tiše
/// nahradí výchozími hodnotami - první spuštění i ruční úprava/smazání souboru
/// tak nikdy nespadne.
/// </summary>
public sealed class JsonAppSettingsStore(string? filePath = null) : IAppSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _filePath = filePath ?? GetDefaultPath();

    public static string GetDefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Diskora", "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, SerializerOptions);
        File.WriteAllText(_filePath, json);
    }
}

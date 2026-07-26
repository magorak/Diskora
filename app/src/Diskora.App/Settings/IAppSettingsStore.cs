namespace Diskora.App.Settings;

public interface IAppSettingsStore
{
    AppSettings Load();

    void Save(AppSettings settings);
}

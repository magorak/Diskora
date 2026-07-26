using Diskora.App.Settings;
using Diskora.App.Theming;

namespace Diskora.App.Tests;

public sealed class ThemeServiceTests
{
    private sealed class FakeSettingsStore(string theme) : IAppSettingsStore
    {
        public AppSettings Load() => new() { Theme = theme };

        public void Save(AppSettings settings)
        {
        }
    }

    [Theory]
    [InlineData("Light", AppTheme.Light)]
    [InlineData("Dark", AppTheme.Dark)]
    [InlineData("System", AppTheme.System)]
    public void LoadSavedTheme_KnownValue_ParsesCorrectly(string stored, AppTheme expected)
    {
        var result = ThemeService.LoadSavedTheme(new FakeSettingsStore(stored));

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Neplatna hodnota")]
    public void LoadSavedTheme_InvalidValue_FallsBackToSystem(string stored)
    {
        var result = ThemeService.LoadSavedTheme(new FakeSettingsStore(stored));

        Assert.Equal(AppTheme.System, result);
    }
}

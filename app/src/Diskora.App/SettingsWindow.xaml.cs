using System.Windows;
using System.Windows.Controls;
using Diskora.App.Settings;

namespace Diskora.App;

/// <summary>
/// Prosté okno bez ViewModelu (žádný stav, který by přežil zavření okna kromě
/// toho, co je uloženo v <see cref="IAppSettingsStore"/>) - "Uložit" zapíše a
/// zavře, "Zrušit"/×/Esc zavře beze změny.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly IAppSettingsStore _settingsStore;

    public SettingsWindow(IAppSettingsStore settingsStore)
    {
        InitializeComponent();
        _settingsStore = settingsStore;

        var settings = _settingsStore.Load();
        SelectComboBoxItemByTag(ThresholdComboBox, settings.NotificationThreshold);
        PromptForElevationCheckBox.IsChecked = settings.PromptForElevationAtStartup;
    }

    private static void SelectComboBoxItemByTag(ComboBox comboBox, string tag)
    {
        foreach (var item in comboBox.Items)
        {
            if (item is ComboBoxItem { } comboBoxItem && (string)comboBoxItem.Tag == tag)
            {
                comboBox.SelectedItem = comboBoxItem;
                return;
            }
        }

        comboBox.SelectedIndex = 0;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var settings = _settingsStore.Load();
        settings.NotificationThreshold = ThresholdComboBox.SelectedItem is ComboBoxItem { } selected
            ? (string)selected.Tag
            : "Warning";
        settings.PromptForElevationAtStartup = PromptForElevationCheckBox.IsChecked == true;
        _settingsStore.Save(settings);

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Diskora.App.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        // Bez tohoto by tlačítka navázaná na RelayCommand zůstávala (zdánlivě)
        // needostupná chvíli po dokončení async operace - CommandManager.
        // RequerySuggested se jinak spoléhá na běžné vstupní události (myš,
        // klávesnice), ne na změny vlastností přišlé z async pokračování.
        // Ověřeno živě - bez tohoto řádku tlačítko "Odpojit" po připojení
        // ISO chvíli nereagovalo na klik.
        CommandManager.InvalidateRequerySuggested();

        return true;
    }
}

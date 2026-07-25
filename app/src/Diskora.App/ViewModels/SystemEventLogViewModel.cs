using System.Collections.ObjectModel;
using System.Windows.Input;
using Diskora.App.Commands;
using Diskora.Core.Services;

namespace Diskora.App.ViewModels;

public sealed class SystemEventLogViewModel : ViewModelBase
{
    private readonly IDiskEventLogService _service;
    private bool _isLoading;
    private string? _errorMessage;

    public SystemEventLogViewModel(IDiskEventLogService service)
    {
        _service = service;
        RefreshCommand = new RelayCommand(Load, () => !IsLoading);
        Load();
    }

    public ObservableCollection<DiskEventLogRowViewModel> Entries { get; } = [];

    public ICommand RefreshCommand { get; }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetField(ref _isLoading, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetField(ref _errorMessage, value);
    }

    public bool HasEntries => Entries.Count > 0;

    private void Load()
    {
        IsLoading = true;
        ErrorMessage = null;
        Entries.Clear();

        try
        {
            foreach (var entry in _service.GetRecentDiskEvents())
            {
                Entries.Add(new DiskEventLogRowViewModel(entry));
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Systémový protokol se nepodařilo načíst: {ex.Message}";
        }

        OnPropertyChanged(nameof(HasEntries));
        IsLoading = false;
    }
}

using System.Collections.ObjectModel;
using System.Windows.Input;
using Diskora.App.Commands;
using Diskora.Core.Models;
using Diskora.Core.Services;

namespace Diskora.App.ViewModels;

public sealed class SmartViewModel : ViewModelBase
{
    private readonly ISmartService _smartService;
    private readonly int _diskIndex;
    private bool _isLoading;
    private bool _isSupported;
    private string? _unavailableReason;
    private DiskHealthStatus _overallHealth = DiskHealthStatus.Unknown;

    public SmartViewModel(ISmartService smartService, int diskIndex, string diskName)
    {
        _smartService = smartService;
        _diskIndex = diskIndex;
        DiskName = diskName;
        RefreshCommand = new RelayCommand(Load, () => !IsLoading);
        Load();
    }

    public string DiskName { get; }

    public ObservableCollection<SmartAttributeRowViewModel> Attributes { get; } = [];

    public ICommand RefreshCommand { get; }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetField(ref _isLoading, value);
    }

    public bool IsSupported
    {
        get => _isSupported;
        private set => SetField(ref _isSupported, value);
    }

    public string? UnavailableReason
    {
        get => _unavailableReason;
        private set => SetField(ref _unavailableReason, value);
    }

    public DiskHealthStatus OverallHealth
    {
        get => _overallHealth;
        private set => SetField(ref _overallHealth, value);
    }

    public string OverallHealthDisplay => OverallHealth switch
    {
        DiskHealthStatus.Healthy => "V pořádku",
        DiskHealthStatus.Warning => "Varování",
        DiskHealthStatus.Critical => "Kritické",
        _ => "Neznámé",
    };

    private void Load()
    {
        IsLoading = true;

        var result = _smartService.ReadReport(_diskIndex);
        IsSupported = result.IsSupported;
        UnavailableReason = result.UnavailableReason;

        Attributes.Clear();
        if (result.Report is not null)
        {
            foreach (var reading in result.Report.Attributes)
            {
                Attributes.Add(new SmartAttributeRowViewModel(reading));
            }

            OverallHealth = result.Report.OverallHealth;
        }
        else
        {
            OverallHealth = DiskHealthStatus.Unknown;
        }

        OnPropertyChanged(nameof(OverallHealthDisplay));
        IsLoading = false;
    }
}

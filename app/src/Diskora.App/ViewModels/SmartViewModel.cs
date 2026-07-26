using System.Collections.ObjectModel;
using System.Windows.Input;
using Diskora.App.Commands;
using Diskora.App.Display;
using Diskora.Core.Models;
using Diskora.Core.Services;
using Diskora.Core.Smart;

namespace Diskora.App.ViewModels;

public sealed class SmartViewModel : ViewModelBase
{
    private readonly ISmartService _smartService;
    private readonly IDiskHistoryStore _historyStore;
    private readonly int _diskIndex;
    private bool _isLoading;
    private bool _isSupported;
    private bool _isNvme;
    private string? _unavailableReason;
    private DiskHealthStatus _overallHealth = DiskHealthStatus.Unknown;

    public SmartViewModel(ISmartService smartService, IDiskHistoryStore historyStore, int diskIndex, string diskName)
    {
        _smartService = smartService;
        _historyStore = historyStore;
        _diskIndex = diskIndex;
        DiskName = diskName;
        RefreshCommand = new RelayCommand(Load, () => !IsLoading);
        Load();
    }

    public string DiskName { get; }

    public ObservableCollection<SmartAttributeRowViewModel> Attributes { get; } = [];

    /// <summary>Zdravotní údaje NVMe disku. Naplněné právě tehdy, když je <see cref="Attributes"/> prázdné - viz SmartReport.</summary>
    public ObservableCollection<NvmeHealthMetricRowViewModel> NvmeMetrics { get; } = [];

    public ObservableCollection<SmartHistoryRowViewModel> History { get; } = [];

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

    /// <summary>Přepíná zobrazení mezi tabulkou ATA atributů a NVMe přehledem.</summary>
    public bool IsNvme
    {
        get => _isNvme;
        private set => SetField(ref _isNvme, value);
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

    public string OverallHealthDisplay => OverallHealth.ToDisplayText();

    private void Load()
    {
        IsLoading = true;

        var result = _smartService.ReadReport(_diskIndex);
        IsSupported = result.IsSupported;
        UnavailableReason = result.UnavailableReason;

        Attributes.Clear();
        NvmeMetrics.Clear();
        if (result.Report is not null)
        {
            foreach (var reading in result.Report.Attributes)
            {
                Attributes.Add(new SmartAttributeRowViewModel(reading));
            }

            if (result.Report.NvmeHealth is { } nvmeHealth)
            {
                foreach (var metric in NvmeHealthCatalog.Describe(nvmeHealth))
                {
                    NvmeMetrics.Add(new NvmeHealthMetricRowViewModel(metric));
                }
            }

            IsNvme = result.Report.NvmeHealth is not null;
            OverallHealth = result.Report.OverallHealth;
        }
        else
        {
            IsNvme = false;
            OverallHealth = DiskHealthStatus.Unknown;
        }

        OnPropertyChanged(nameof(OverallHealthDisplay));

        History.Clear();
        foreach (var entry in _historyStore.GetRecentSmartHistory(_diskIndex))
        {
            History.Add(new SmartHistoryRowViewModel(entry));
        }

        IsLoading = false;
    }
}

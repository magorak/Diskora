using System.Collections.ObjectModel;
using System.Windows.Input;
using Diskora.App.Commands;
using Diskora.Core.Services;

namespace Diskora.App.ViewModels;

public sealed class OptimizationViewModel : ViewModelBase
{
    private readonly IDiskOptimizationService _service;
    private readonly string _driveLetter;
    private CancellationTokenSource? _runCts;
    private bool _isRunning;
    private bool? _isSolidState;
    private bool? _supportsTrim;
    private string? _operationSummary;

    public OptimizationViewModel(IDiskOptimizationService service, string driveLetter, string volumeName)
    {
        _service = service;
        _driveLetter = driveLetter;
        VolumeName = volumeName;

        RefreshCapabilitiesCommand = new RelayCommand(RefreshCapabilities, () => !IsRunning);
        RunTrimCommand = new RelayCommand(async () => await RunAsync(trim: true), () => !IsRunning);
        RunDefragmentCommand = new RelayCommand(async () => await RunAsync(trim: false), () => !IsRunning);
        CancelCommand = new RelayCommand(Cancel, () => IsRunning);

        RefreshCapabilities();
    }

    public string VolumeName { get; }

    public ObservableCollection<string> OutputLines { get; } = [];

    public ICommand RefreshCapabilitiesCommand { get; }

    public ICommand RunTrimCommand { get; }

    public ICommand RunDefragmentCommand { get; }

    public ICommand CancelCommand { get; }

    public bool IsRunning
    {
        get => _isRunning;
        private set => SetField(ref _isRunning, value);
    }

    public bool? IsSolidState
    {
        get => _isSolidState;
        private set
        {
            if (SetField(ref _isSolidState, value))
            {
                OnPropertyChanged(nameof(ShowTrimAction));
                OnPropertyChanged(nameof(ShowDefragAction));
                OnPropertyChanged(nameof(CapabilitiesSummary));
            }
        }
    }

    public bool? SupportsTrim
    {
        get => _supportsTrim;
        private set
        {
            if (SetField(ref _supportsTrim, value))
            {
                OnPropertyChanged(nameof(CapabilitiesSummary));
            }
        }
    }

    public bool ShowTrimAction => IsSolidState == true;

    public bool ShowDefragAction => IsSolidState == false;

    public string CapabilitiesSummary => IsSolidState switch
    {
        true => SupportsTrim == true
            ? "SSD s podporou TRIM."
            : "SSD (podporu TRIM se nepodařilo ověřit).",
        false => "Rotační pevný disk (HDD) - vhodná tradiční defragmentace.",
        null => "Typ disku se nepodařilo zjistit (obvykle chybí práva administrátora na systémovém svazku).",
    };

    public string? OperationSummary
    {
        get => _operationSummary;
        private set => SetField(ref _operationSummary, value);
    }

    private void RefreshCapabilities()
    {
        var capabilities = _service.GetCapabilities(_driveLetter);
        IsSolidState = capabilities.IsLikelySolidState;
        SupportsTrim = capabilities.SupportsTrim;
    }

    private async Task RunAsync(bool trim)
    {
        OutputLines.Clear();
        OperationSummary = null;
        IsRunning = true;
        _runCts = new CancellationTokenSource();

        var progress = new Progress<string>(line => OutputLines.Add(line));

        try
        {
            var outcome = trim
                ? await _service.RunTrimAsync(_driveLetter, progress, _runCts.Token)
                : await _service.RunDefragmentAsync(_driveLetter, progress, _runCts.Token);

            OperationSummary = outcome.Started
                ? $"Dokončeno (návratový kód {outcome.ExitCode})."
                : outcome.FailureReason;
        }
        finally
        {
            IsRunning = false;
        }
    }

    private void Cancel() => _runCts?.Cancel();
}

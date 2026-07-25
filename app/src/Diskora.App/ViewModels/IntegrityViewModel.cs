using System.Collections.ObjectModel;
using System.Windows.Input;
using Diskora.App.Commands;
using Diskora.App.Parsing;
using Diskora.Core.Models;
using Diskora.Core.Services;

namespace Diskora.App.ViewModels;

public sealed class IntegrityViewModel : ViewModelBase
{
    // chkdsk /scan (bez /r) proběhne fázemi 1-3; fáze 4-5 (kontrola sektorů)
    // se spouští jen s /r, který zatím Diskora nenabízí (viz TODO - skutečná
    // oprava je záměrně samostatný krok).
    private const int TotalStages = 3;

    private readonly IIntegrityCheckService _service;
    private readonly IDiskHistoryStore _historyStore;
    private readonly string _driveLetter;
    private CancellationTokenSource? _scanCts;
    private VolumeDirtyState _dirtyState = VolumeDirtyState.Unknown;
    private bool _isScanning;
    private IntegrityScanOutcome? _lastOutcome;
    private int? _currentStage;
    private string? _currentStageDescription;
    private double _progressPercent;

    public IntegrityViewModel(IIntegrityCheckService service, IDiskHistoryStore historyStore, string driveLetter, string volumeName)
    {
        _service = service;
        _historyStore = historyStore;
        _driveLetter = driveLetter;
        VolumeName = volumeName;

        RefreshDirtyStateCommand = new RelayCommand(RefreshDirtyState, () => !IsScanning);
        StartScanCommand = new RelayCommand(async () => await StartScanAsync(), () => !IsScanning);
        CancelScanCommand = new RelayCommand(CancelScan, () => IsScanning);

        RefreshDirtyState();
    }

    public string VolumeName { get; }

    public ObservableCollection<string> OutputLines { get; } = [];

    public ObservableCollection<IntegrityHistoryRowViewModel> History { get; } = [];

    public ICommand RefreshDirtyStateCommand { get; }

    public ICommand StartScanCommand { get; }

    public ICommand CancelScanCommand { get; }

    public VolumeDirtyState DirtyState
    {
        get => _dirtyState;
        private set
        {
            if (SetField(ref _dirtyState, value))
            {
                OnPropertyChanged(nameof(DirtyStateDisplay));
            }
        }
    }

    public string DirtyStateDisplay => DirtyState switch
    {
        VolumeDirtyState.Clean => "Svazek je v pořádku (dirty bit není nastaven)",
        VolumeDirtyState.Dirty => "Svazek je označen jako poškozený - doporučena kontrola",
        _ => "Stav se nepodařilo zjistit",
    };

    public bool IsScanning
    {
        get => _isScanning;
        private set => SetField(ref _isScanning, value);
    }

    public string? CurrentStageDescription
    {
        get => _currentStageDescription;
        private set => SetField(ref _currentStageDescription, value);
    }

    public double ProgressPercent
    {
        get => _progressPercent;
        private set => SetField(ref _progressPercent, value);
    }

    public string? ScanSummary => _lastOutcome switch
    {
        null => null,
        { Started: false } => _lastOutcome.FailureReason,
        { AppearsClean: true } => "Kontrola dokončena - žádné chyby nenalezeny.",
        _ => $"Kontrola dokončena (návratový kód {_lastOutcome.ExitCode}). Zkontrolujte výstup níže.",
    };

    private void RefreshDirtyState()
    {
        DirtyState = _service.CheckDirtyState(_driveLetter);
        LoadHistory();
    }

    private void LoadHistory()
    {
        History.Clear();
        foreach (var entry in _historyStore.GetRecentIntegrityHistory(_driveLetter))
        {
            History.Add(new IntegrityHistoryRowViewModel(entry));
        }
    }

    private async Task StartScanAsync()
    {
        OutputLines.Clear();
        _lastOutcome = null;
        OnPropertyChanged(nameof(ScanSummary));
        IsScanning = true;
        _currentStage = null;
        CurrentStageDescription = "Spouštím kontrolu...";
        ProgressPercent = 0;
        _scanCts = new CancellationTokenSource();

        var progress = new Progress<string>(OnOutputLine);

        try
        {
            _lastOutcome = await _service.RunReadOnlyScanAsync(_driveLetter, progress, _scanCts.Token);
        }
        finally
        {
            IsScanning = false;

            if (_lastOutcome?.Started == true)
            {
                ProgressPercent = 100;
                CurrentStageDescription = "Dokončeno";
            }

            OnPropertyChanged(nameof(ScanSummary));
            RefreshDirtyState();
        }
    }

    private void OnOutputLine(string line)
    {
        OutputLines.Add(line);

        var stage = ChkdskOutputParser.TryParseStage(line);
        if (stage is not null)
        {
            _currentStage = stage;
            CurrentStageDescription = ChkdskOutputParser.GetStageDescription(stage.Value);
            ProgressPercent = StageBaselinePercent(stage.Value);
        }

        var percent = ChkdskOutputParser.TryParsePercent(line);
        if (percent is not null && _currentStage is int currentStage)
        {
            var baseline = StageBaselinePercent(currentStage);
            var stageSpan = 100.0 / TotalStages;
            ProgressPercent = Math.Clamp(baseline + (percent.Value / 100.0 * stageSpan), 0, 100);
        }
    }

    private static double StageBaselinePercent(int stage) =>
        Math.Clamp((stage - 1) * (100.0 / TotalStages), 0, 100);

    private void CancelScan() => _scanCts?.Cancel();
}

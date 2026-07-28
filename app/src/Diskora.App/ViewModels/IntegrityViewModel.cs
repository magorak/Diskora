using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Diskora.App.Commands;
using Diskora.App.Parsing;
using Diskora.Core.Models;
using Diskora.Core.Output;
using Diskora.Core.Services;

namespace Diskora.App.ViewModels;

public sealed class IntegrityViewModel : ViewModelBase
{
    // chkdsk /scan (bez /r) proběhne fázemi 1-3; fáze 4-5 (kontrola sektorů)
    // se spouští jen s /r, který Diskora zatím nenabízí (vyžadoval by řešit
    // naplánovaný restart na systémovém svazku - viz RunSpotFixAsync níže,
    // který se tomu záměrně vyhýbá).
    private const int TotalStages = 3;

    private readonly IIntegrityCheckService _service;
    private readonly IDiskHistoryStore _historyStore;
    private readonly string _driveLetter;
    private CancellationTokenSource? _scanCts;
    private CancellationTokenSource? _repairCts;
    private VolumeDirtyState _dirtyState = VolumeDirtyState.Unknown;
    private bool _isScanning;
    private bool _showRawOutput;
    private bool _isRepairing;
    private IntegrityScanOutcome? _lastOutcome;
    private IntegrityScanOutcome? _lastRepairOutcome;
    private int? _currentStage;
    private string? _currentStageDescription;
    private double _progressPercent;

    public IntegrityViewModel(IIntegrityCheckService service, IDiskHistoryStore historyStore, string driveLetter, string volumeName)
    {
        _service = service;
        _historyStore = historyStore;
        _driveLetter = driveLetter;
        VolumeName = volumeName;

        RefreshDirtyStateCommand = new RelayCommand(RefreshDirtyState, () => !IsBusy);
        StartScanCommand = new RelayCommand(async () => await StartScanAsync(), () => !IsBusy);
        CancelScanCommand = new RelayCommand(CancelScan, () => IsScanning);
        RunSpotFixCommand = new RelayCommand(async () => await RunSpotFixAsync(), () => !IsBusy);
        CancelSpotFixCommand = new RelayCommand(CancelSpotFix, () => IsRepairing);

        RefreshDirtyState();
    }

    public string VolumeName { get; }

    /// <summary>Výpis přeložený do češtiny, bez záplavy průběžných hlášení.</summary>
    public ObservableCollection<string> OutputLines { get; } = [];

    /// <summary>Syrový výstup nástroje tak, jak přišel - přepínatelný v okně.</summary>
    public ObservableCollection<string> RawOutputLines { get; } = [];

    /// <summary>Přepíná zobrazený výpis mezi překladem a originálem.</summary>
    public bool ShowRawOutput
    {
        get => _showRawOutput;
        set
        {
            if (SetField(ref _showRawOutput, value))
            {
                OnPropertyChanged(nameof(DisplayedOutputLines));
            }
        }
    }

    public ObservableCollection<string> DisplayedOutputLines => ShowRawOutput ? RawOutputLines : OutputLines;

    public ObservableCollection<IntegrityHistoryRowViewModel> History { get; } = [];

    public ICommand RefreshDirtyStateCommand { get; }

    public ICommand StartScanCommand { get; }

    public ICommand CancelScanCommand { get; }

    public ICommand RunSpotFixCommand { get; }

    public ICommand CancelSpotFixCommand { get; }

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
        private set
        {
            if (SetField(ref _isScanning, value))
            {
                OnPropertyChanged(nameof(IsBusy));
            }
        }
    }

    public bool IsRepairing
    {
        get => _isRepairing;
        private set
        {
            if (SetField(ref _isRepairing, value))
            {
                OnPropertyChanged(nameof(IsBusy));
            }
        }
    }

    /// <summary>Kontrola i oprava sdílejí stejný výstupní panel - nemají běžet zároveň.</summary>
    public bool IsBusy => IsScanning || IsRepairing;

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

    public string? RepairSummary => _lastRepairOutcome switch
    {
        null => null,
        { Started: false } => _lastRepairOutcome.FailureReason,
        { AppearsClean: true } => "Oprava dokončena.",
        _ => $"Oprava dokončena (návratový kód {_lastRepairOutcome.ExitCode}). Zkontrolujte výstup níže.",
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
        RawOutputLines.Clear();
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
        // Syrový originál se schovává vždycky - překlad je výchozí zobrazení,
        // ne náhrada. Uživatel si může kdykoli ověřit, co nástroj skutečně řekl.
        RawOutputLines.Add(line);

        // Řádků „Progress: ... done; ..." vypíše chkdsk stovky. Postup z nich
        // čteme, ale do výpisu nepatří - jen by ho zaplavily.
        if (!ToolOutputTranslator.IsProgressNoise(line))
        {
            OutputLines.Add(ToolOutputTranslator.Translate(line));
        }

        var stage = ChkdskOutputParser.TryParseStage(line);
        if (stage is not null)
        {
            _currentStage = stage;
            CurrentStageDescription = ChkdskOutputParser.GetStageDescription(stage.Value);
            ProgressPercent = StageBaselinePercent(stage.Value);
        }

        // Celkový postup hlášený přímo chkdskem má přednost - ví o délce fází víc
        // než náš odhad z pořadí fáze.
        if (ChkdskOutputParser.TryParseOverallPercent(line) is { } overallPercent)
        {
            ProgressPercent = overallPercent;
            return;
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

    /// <summary>
    /// Na rozdíl od <see cref="StartScanAsync"/> (needestruktivní, jen čte) tahle akce
    /// SKUTEČNĚ ZAPISUJE opravy na disk - proto vlastní explicitní potvrzení PŘED
    /// zavoláním služby, přesně jak slibuje popisek v okně Kontrola integrity.
    /// </summary>
    private async Task RunSpotFixAsync()
    {
        // MessageBoxResult.No jako výchozí tlačítko - náhodný Enter/mezerník (nebo focus
        // zděděný z předchozího dialogu) tak nemůže omylem potvrdit akci, která SKUTEČNĚ
        // zapisuje na disk. "Ano" musí být vždy vědomá volba myší/šipkami+Enter.
        var confirmed = MessageBox.Show(
            $"Spotfix se pokusí opravit běžné problémy na svazku {VolumeName} (poškozené indexy, " +
            "osiřelé soubory, bezpečnostní deskriptory) bez nutnosti svazek odpojit - na rozdíl od " +
            "kontroly „jen čtení“ výše ale tahle akce SKUTEČNĚ ZAPISUJE opravy na disk. " +
            "Ve vzácných případech si i spotfix může vyžádat restart počítače. Pokračovat?",
            "Potvrdit opravu (spotfix)",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;

        if (!confirmed)
        {
            return;
        }

        OutputLines.Clear();
        RawOutputLines.Clear();
        _lastRepairOutcome = null;
        OnPropertyChanged(nameof(RepairSummary));
        IsRepairing = true;
        _repairCts = new CancellationTokenSource();

        var progress = new Progress<string>(line => OutputLines.Add(line));

        try
        {
            _lastRepairOutcome = await _service.RunSpotFixAsync(_driveLetter, progress, _repairCts.Token);
        }
        finally
        {
            IsRepairing = false;
            OnPropertyChanged(nameof(RepairSummary));
            RefreshDirtyState();
        }
    }

    private void CancelSpotFix() => _repairCts?.Cancel();
}

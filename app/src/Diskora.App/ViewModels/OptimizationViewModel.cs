using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Diskora.App.Commands;
using Diskora.Core.Services;

namespace Diskora.App.ViewModels;

public sealed class OptimizationViewModel : ViewModelBase
{
    private readonly IDiskOptimizationService _service;
    private readonly IFragmentationAnalysisService _fragmentationService;
    private readonly string _driveLetter;
    private CancellationTokenSource? _runCts;
    private CancellationTokenSource? _fragmentationCts;
    private bool _isRunning;
    private bool? _isSolidState;
    private bool? _supportsTrim;
    private string? _operationSummary;
    private bool _isAnalyzingFragmentation;
    private string _fragmentationStatusText = string.Empty;
    private string _fragmentationSummary = string.Empty;
    private bool _hasAnalyzedFragmentation;

    public OptimizationViewModel(
        IDiskOptimizationService service, IFragmentationAnalysisService fragmentationService, string driveLetter, string volumeName)
    {
        _service = service;
        _fragmentationService = fragmentationService;
        _driveLetter = driveLetter;
        VolumeName = volumeName;

        RefreshCapabilitiesCommand = new RelayCommand(RefreshCapabilities, () => !IsRunning);
        RunTrimCommand = new RelayCommand(async () => await RunAsync(trim: true), () => !IsRunning);
        RunDefragmentCommand = new RelayCommand(async () => await RunAsync(trim: false), () => !IsRunning);
        CancelCommand = new RelayCommand(Cancel, () => IsRunning);
        AnalyzeFragmentationCommand = new RelayCommand(async () => await AnalyzeFragmentationAsync(), () => !IsAnalyzingFragmentation);
        CancelFragmentationCommand = new RelayCommand(CancelFragmentationAnalysis, () => IsAnalyzingFragmentation);

        RefreshCapabilities();
    }

    public string VolumeName { get; }

    public ObservableCollection<string> OutputLines { get; } = [];

    public ICommand RefreshCapabilitiesCommand { get; }

    public ICommand RunTrimCommand { get; }

    public ICommand RunDefragmentCommand { get; }

    public ICommand CancelCommand { get; }

    public ICommand AnalyzeFragmentationCommand { get; }

    public ICommand CancelFragmentationCommand { get; }

    public ObservableCollection<FragmentedFileRowViewModel> MostFragmentedFiles { get; } = [];

    public bool IsAnalyzingFragmentation
    {
        get => _isAnalyzingFragmentation;
        private set => SetField(ref _isAnalyzingFragmentation, value);
    }

    public string FragmentationStatusText
    {
        get => _fragmentationStatusText;
        private set => SetField(ref _fragmentationStatusText, value);
    }

    public string FragmentationSummary
    {
        get => _fragmentationSummary;
        private set => SetField(ref _fragmentationSummary, value);
    }

    public bool HasAnalyzedFragmentation
    {
        get => _hasAnalyzedFragmentation;
        private set => SetField(ref _hasAnalyzedFragmentation, value);
    }

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

    private async Task AnalyzeFragmentationAsync()
    {
        MostFragmentedFiles.Clear();
        FragmentationSummary = string.Empty;
        FragmentationStatusText = string.Empty;
        IsAnalyzingFragmentation = true;
        _fragmentationCts = new CancellationTokenSource();

        var progress = new Progress<string>(path => FragmentationStatusText = path);

        try
        {
            var result = await _fragmentationService.AnalyzeAsync(_driveLetter, progress, _fragmentationCts.Token);

            foreach (var entry in result.MostFragmentedFiles)
            {
                MostFragmentedFiles.Add(new FragmentedFileRowViewModel(entry));
            }

            FragmentationSummary = result.FilesScanned == 0
                ? "Žádné soubory k analýze."
                : $"{result.FragmentedFileCount} z {result.FilesScanned} souborů je fragmentovaných.";
        }
        catch (OperationCanceledException)
        {
            FragmentationSummary = "Analýza zrušena.";
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            FragmentationSummary = $"Analýzu se nepodařilo dokončit: {ex.Message}";
        }
        finally
        {
            HasAnalyzedFragmentation = true;
            FragmentationStatusText = string.Empty;
            IsAnalyzingFragmentation = false;
        }
    }

    private void CancelFragmentationAnalysis() => _fragmentationCts?.Cancel();
}

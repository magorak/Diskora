using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Diskora.App.Commands;
using Diskora.App.Display;
using Diskora.Core.Models;
using Diskora.Core.Services;

namespace Diskora.App.ViewModels;

public sealed class DiskDoctorViewModel : ViewModelBase
{
    private readonly IDiskDoctorService _doctorService;
    private readonly string _driveLetter;
    private readonly int? _physicalDiskIndex;
    private bool _isRunning;
    private bool _hasRun;
    private DiskDoctorSeverity _overall = DiskDoctorSeverity.Ok;
    private string? _errorMessage;

    public DiskDoctorViewModel(
        IDiskDoctorService doctorService,
        string driveLetter,
        int? physicalDiskIndex,
        string subject)
    {
        _doctorService = doctorService;
        _driveLetter = driveLetter;
        _physicalDiskIndex = physicalDiskIndex;
        Subject = subject;
        RunCommand = new RelayCommand(() => _ = RunAsync(), () => !IsRunning);
    }

    public string Subject { get; }

    public ObservableCollection<DiskDoctorFindingRowViewModel> Findings { get; } = [];

    public ICommand RunCommand { get; }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            SetField(ref _isRunning, value);
            OnPropertyChanged(nameof(StatusText));
        }
    }

    /// <summary>Celkový verdikt se ukazuje až po doběhnutí, ať nesvítí zelená dřív, než se cokoli změřilo.</summary>
    public bool HasRun
    {
        get => _hasRun;
        private set => SetField(ref _hasRun, value);
    }

    public DiskDoctorSeverity Overall
    {
        get => _overall;
        private set
        {
            SetField(ref _overall, value);
            OnPropertyChanged(nameof(OverallDisplay));
            OnPropertyChanged(nameof(VerdictText));
        }
    }

    public string OverallDisplay => Overall.ToDisplayText();

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetField(ref _errorMessage, value);
    }

    public string StatusText => IsRunning ? "Probíhá kontrola..." : "Kontrola je needestruktivní - jen čte, nic nemění.";

    public string VerdictText => Overall switch
    {
        DiskDoctorSeverity.Critical => "Disk hlásí vážný problém. Zálohujte data hned.",
        DiskDoctorSeverity.Warning => "Něco si zaslouží pozornost, ale nehoří to.",
        DiskDoctorSeverity.Info => "Nic špatného. Níže je pár věcí, které stojí za zmínku.",
        _ => "Všechno, co jde zkontrolovat, vyšlo v pořádku.",
    };

    public async Task RunAsync()
    {
        IsRunning = true;
        ErrorMessage = null;

        try
        {
            var report = await _doctorService.RunAsync(_driveLetter, _physicalDiskIndex, Subject).ConfigureAwait(true);

            Findings.Clear();
            foreach (var finding in report.Findings)
            {
                Findings.Add(new DiskDoctorFindingRowViewModel(finding));
            }

            Overall = report.Overall;
            HasRun = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ErrorMessage = $"Kontrolu se nepodařilo dokončit: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
        }
    }
}

public sealed class DiskDoctorFindingRowViewModel(DiskDoctorFinding finding)
{
    public string Title { get; } = finding.Title;

    public string Detail { get; } = finding.Detail;

    public DiskDoctorSeverity Severity { get; } = finding.Severity;

    public string SeverityDisplay { get; } = finding.Severity.ToDisplayText();

    public DiskDoctorAction Action { get; } = finding.RecommendedAction;

    public string ActionButtonText { get; } = finding.RecommendedAction.ToButtonText();

    /// <summary>Rada bez tlačítka (zálohovat, zkontrolovat kabel, spustit jako správce) není chyba - jen nemá kam vést.</summary>
    public bool HasActionButton { get; } = finding.RecommendedAction.ToButtonText().Length > 0;
}

using System.Windows.Input;
using Diskora.App.Commands;
using Diskora.Core.Formatting;
using Diskora.Core.Models;
using Diskora.Core.Services;

namespace Diskora.App.ViewModels;

public sealed class CapacityTestViewModel : ViewModelBase
{
    private readonly ICapacityTestService _service;
    private readonly string _driveLetter;
    private CancellationTokenSource? _cts;
    private bool _isRunning;
    private double _progressPercent;
    private string _phaseText = string.Empty;
    private string? _resultText;
    private bool _hasResult;
    private bool _resultIsGood;

    public CapacityTestViewModel(ICapacityTestService service, string driveLetter, string volumeName)
    {
        _service = service;
        _driveLetter = driveLetter;
        VolumeName = volumeName;

        StartCommand = new RelayCommand(async () => await RunAsync(), () => !IsRunning);
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsRunning);
    }

    public string VolumeName { get; }

    public ICommand StartCommand { get; }

    public ICommand CancelCommand { get; }

    public bool IsRunning
    {
        get => _isRunning;
        private set => SetField(ref _isRunning, value);
    }

    public double ProgressPercent
    {
        get => _progressPercent;
        private set => SetField(ref _progressPercent, value);
    }

    public string PhaseText
    {
        get => _phaseText;
        private set => SetField(ref _phaseText, value);
    }

    public string? ResultText
    {
        get => _resultText;
        private set => SetField(ref _resultText, value);
    }

    public bool HasResult
    {
        get => _hasResult;
        private set => SetField(ref _hasResult, value);
    }

    /// <summary>Řídí barvu shrnutí - zelená jen když disk skutečně obstál.</summary>
    public bool ResultIsGood
    {
        get => _resultIsGood;
        private set => SetField(ref _resultIsGood, value);
    }

    private async Task RunAsync()
    {
        IsRunning = true;
        HasResult = false;
        ResultText = null;
        ProgressPercent = 0;
        _cts = new CancellationTokenSource();

        var progress = new Progress<CapacityTestProgress>(p =>
        {
            ProgressPercent = p.Percent;
            PhaseText = p.Phase switch
            {
                CapacityTestPhase.Writing => $"Zapisuji vzor... {ByteSizeFormatter.Format(p.BytesProcessed)}",
                CapacityTestPhase.Verifying => $"Čtu zpátky a porovnávám... {ByteSizeFormatter.Format(p.BytesProcessed)}",
                _ => "Uklízím testovací data...",
            };
        });

        var result = await _service.RunAsync(_driveLetter, progress, _cts.Token);

        IsRunning = false;
        PhaseText = string.Empty;
        HasResult = true;
        ResultIsGood = result.DataIsIntact;
        ResultText = Describe(result);
    }

    private static string Describe(CapacityTestResult result)
    {
        if (!result.Completed)
        {
            return result.FailureReason ?? "Test se nepodařilo dokončit.";
        }

        if (result.DataIsIntact)
        {
            return $"Disk obstál. Zapsáno i přečteno {ByteSizeFormatter.Format(result.BytesWritten)} beze změny, " +
                   "takže v testované části skutečně pojme to, co tvrdí.";
        }

        return $"POZOR: data se změnila po {ByteSizeFormatter.Format(result.FirstMismatchOffset ?? 0)}. " +
               "Disk hlásí víc kapacity, než skutečně má, nebo je vadný. Nepoužívejte ho pro nic, o co nechcete přijít.";
    }
}

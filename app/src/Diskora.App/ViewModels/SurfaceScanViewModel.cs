using System.Collections.ObjectModel;
using System.Windows.Input;
using Diskora.App.Commands;
using Diskora.Core.Formatting;
using Diskora.Core.Models;
using Diskora.Core.Services;

namespace Diskora.App.ViewModels;

public sealed class SurfaceScanViewModel : ViewModelBase
{
    private readonly ISurfaceScanService _service;
    private readonly int _physicalDiskIndex;
    private readonly long _sizeBytes;
    private CancellationTokenSource? _scanCts;
    private bool _isScanning;
    private double _progressPercent;
    private SurfaceScanResult? _lastResult;

    public SurfaceScanViewModel(ISurfaceScanService service, int physicalDiskIndex, string diskName, long sizeBytes)
    {
        _service = service;
        _physicalDiskIndex = physicalDiskIndex;
        _sizeBytes = sizeBytes;
        DiskName = diskName;

        StartScanCommand = new RelayCommand(async () => await StartScanAsync(), () => !IsScanning);
        CancelScanCommand = new RelayCommand(CancelScan, () => IsScanning);
    }

    public string DiskName { get; }

    public ObservableCollection<string> BadRangeRows { get; } = [];

    public ICommand StartScanCommand { get; }

    public ICommand CancelScanCommand { get; }

    public bool IsScanning
    {
        get => _isScanning;
        private set => SetField(ref _isScanning, value);
    }

    public double ProgressPercent
    {
        get => _progressPercent;
        private set => SetField(ref _progressPercent, value);
    }

    public string? Summary => _lastResult switch
    {
        null => null,
        { Started: false } result => result.FailureReason,
        { AppearsClean: true } => "Sken dokončen - žádné nečitelné oblasti nenalezeny.",
        { } result => $"Sken dokončen - nalezeno {result.BadRanges.Count} nečitelných oblastí (viz seznam níže).",
    };

    private async Task StartScanAsync()
    {
        BadRangeRows.Clear();
        _lastResult = null;
        OnPropertyChanged(nameof(Summary));
        IsScanning = true;
        ProgressPercent = 0;
        _scanCts = new CancellationTokenSource();

        var progress = new Progress<double>(p => ProgressPercent = p);

        try
        {
            _lastResult = await _service.ScanAsync(_physicalDiskIndex, _sizeBytes, progress, _scanCts.Token);

            foreach (var range in _lastResult.BadRanges)
            {
                BadRangeRows.Add($"{ByteSizeFormatter.Format(range.OffsetBytes)} – {ByteSizeFormatter.Format(range.OffsetBytes + range.LengthBytes)}");
            }
        }
        catch (OperationCanceledException)
        {
            _lastResult = new SurfaceScanResult(false, "Sken byl zrušen.", 0, _sizeBytes, []);
        }
        finally
        {
            IsScanning = false;
            ProgressPercent = _lastResult?.Started == true ? 100 : ProgressPercent;
            OnPropertyChanged(nameof(Summary));
        }
    }

    private void CancelScan() => _scanCts?.Cancel();
}

using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Diskora.App.Commands;
using Diskora.Core.Formatting;
using Diskora.Core.Models;
using Diskora.Core.Services;

namespace Diskora.App.ViewModels;

public sealed class DiskUsageViewModel : ViewModelBase
{
    private readonly IDiskUsageScanner _scanner;
    private readonly string _rootPath;
    private readonly List<DirectoryUsageNode> _navigationStack = [];

    private bool _isScanning;
    private string? _errorMessage;
    private string _currentPathText = string.Empty;
    private string _currentSummaryText = string.Empty;
    private string _scanningStatusText = string.Empty;

    public DiskUsageViewModel(IDiskUsageScanner scanner, string rootPath)
    {
        _scanner = scanner;
        _rootPath = rootPath;

        RescanCommand = new RelayCommand(async () => await ScanAsync(), () => !IsScanning);
        NavigateUpCommand = new RelayCommand(NavigateUp, () => CanNavigateUp);

        _ = ScanAsync();
    }

    public ObservableCollection<DiskUsageNodeRowViewModel> Items { get; } = [];

    public ICommand RescanCommand { get; }

    public ICommand NavigateUpCommand { get; }

    public bool IsScanning
    {
        get => _isScanning;
        private set => SetField(ref _isScanning, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetField(ref _errorMessage, value);
    }

    public string CurrentPathText
    {
        get => _currentPathText;
        private set => SetField(ref _currentPathText, value);
    }

    public string CurrentSummaryText
    {
        get => _currentSummaryText;
        private set => SetField(ref _currentSummaryText, value);
    }

    public string ScanningStatusText
    {
        get => _scanningStatusText;
        private set => SetField(ref _scanningStatusText, value);
    }

    public bool CanNavigateUp => _navigationStack.Count > 1;

    public void NavigateInto(DiskUsageNodeRowViewModel row)
    {
        if (!row.CanNavigateInto)
        {
            return;
        }

        _navigationStack.Add(row.Node);
        ShowCurrentLevel();
    }

    private void NavigateUp()
    {
        if (!CanNavigateUp)
        {
            return;
        }

        _navigationStack.RemoveAt(_navigationStack.Count - 1);
        ShowCurrentLevel();
    }

    private async Task ScanAsync()
    {
        IsScanning = true;
        ErrorMessage = null;
        Items.Clear();
        _navigationStack.Clear();
        CurrentPathText = _rootPath;
        CurrentSummaryText = string.Empty;

        var progress = new Progress<string>(path => ScanningStatusText = path);

        try
        {
            var rootNode = await _scanner.ScanAsync(_rootPath, progress);
            _navigationStack.Add(rootNode);
            ShowCurrentLevel();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            ErrorMessage = $"Analýzu se nepodařilo dokončit: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
            ScanningStatusText = string.Empty;
        }
    }

    private void ShowCurrentLevel()
    {
        var current = _navigationStack[^1];
        CurrentPathText = current.FullPath;
        CurrentSummaryText = current.HadAccessError
            ? "Přístup k této složce byl odepřen."
            : $"{ByteSizeFormatter.Format(current.SizeBytes)} celkem, {current.FileCount} souborů, {current.Subdirectories.Count} podsložek";

        Items.Clear();
        foreach (var child in current.Subdirectories.OrderByDescending(n => n.SizeBytes))
        {
            Items.Add(new DiskUsageNodeRowViewModel(child, current.SizeBytes));
        }

        OnPropertyChanged(nameof(CanNavigateUp));
    }
}

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
    private readonly IDuplicateFileFinder _duplicateFinder;
    private readonly string _rootPath;
    private readonly List<DirectoryUsageNode> _navigationStack = [];

    private bool _isScanning;
    private string? _errorMessage;
    private string _currentPathText = string.Empty;
    private string _currentSummaryText = string.Empty;
    private string _scanningStatusText = string.Empty;
    private bool _isFindingDuplicates;
    private string _duplicatesStatusText = string.Empty;
    private string _duplicatesSummaryText = string.Empty;

    public DiskUsageViewModel(IDiskUsageScanner scanner, IDuplicateFileFinder duplicateFinder, string rootPath)
    {
        _scanner = scanner;
        _duplicateFinder = duplicateFinder;
        _rootPath = rootPath;

        RescanCommand = new RelayCommand(async () => await ScanAsync(), () => !IsScanning);
        NavigateUpCommand = new RelayCommand(NavigateUp, () => CanNavigateUp);
        FindDuplicatesCommand = new RelayCommand(async () => await FindDuplicatesAsync(), () => !IsFindingDuplicates && !IsScanning);

        _ = ScanAsync();
    }

    public ObservableCollection<DiskUsageNodeRowViewModel> Items { get; } = [];

    public ObservableCollection<CompositionSegmentViewModel> CompositionSegments { get; } = [];

    public ObservableCollection<TreemapCellViewModel> TreemapCells { get; } = [];

    public ObservableCollection<FileUsageRowViewModel> LargestFiles { get; } = [];

    public ObservableCollection<FileUsageRowViewModel> OldestFiles { get; } = [];

    public ObservableCollection<DuplicateFileRowViewModel> DuplicateFiles { get; } = [];

    public ICommand RescanCommand { get; }

    public ICommand NavigateUpCommand { get; }

    public ICommand FindDuplicatesCommand { get; }

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

    public bool IsFindingDuplicates
    {
        get => _isFindingDuplicates;
        private set => SetField(ref _isFindingDuplicates, value);
    }

    public string DuplicatesStatusText
    {
        get => _duplicatesStatusText;
        private set => SetField(ref _duplicatesStatusText, value);
    }

    public string DuplicatesSummaryText
    {
        get => _duplicatesSummaryText;
        private set => SetField(ref _duplicatesSummaryText, value);
    }

    public bool HasDuplicateFiles => DuplicateFiles.Count > 0;

    public bool HasSearchedForDuplicates { get; private set; }

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
        LargestFiles.Clear();
        OldestFiles.Clear();
        DuplicateFiles.Clear();
        DuplicatesSummaryText = string.Empty;
        HasSearchedForDuplicates = false;
        OnPropertyChanged(nameof(HasDuplicateFiles));
        OnPropertyChanged(nameof(HasSearchedForDuplicates));
        _navigationStack.Clear();
        CurrentPathText = _rootPath;
        CurrentSummaryText = string.Empty;

        var progress = new Progress<string>(path => ScanningStatusText = path);

        try
        {
            var result = await _scanner.ScanAsync(_rootPath, progress);
            _navigationStack.Add(result.Root);
            ShowCurrentLevel();

            LargestFiles.Clear();
            foreach (var entry in result.LargestFiles)
            {
                LargestFiles.Add(new FileUsageRowViewModel(entry));
            }

            OldestFiles.Clear();
            foreach (var entry in result.OldestFiles)
            {
                OldestFiles.Add(new FileUsageRowViewModel(entry));
            }
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

    private async Task FindDuplicatesAsync()
    {
        IsFindingDuplicates = true;
        DuplicatesSummaryText = string.Empty;
        DuplicatesStatusText = string.Empty;
        DuplicateFiles.Clear();
        OnPropertyChanged(nameof(HasDuplicateFiles));

        var progress = new Progress<string>(path => DuplicatesStatusText = path);

        try
        {
            var groups = await _duplicateFinder.FindAsync(_rootPath, progress);

            var groupNumber = 1;
            long totalReclaimable = 0;
            foreach (var group in groups)
            {
                totalReclaimable += group.ReclaimableBytes;
                foreach (var path in group.FilePaths)
                {
                    DuplicateFiles.Add(new DuplicateFileRowViewModel(groupNumber, group.SizeBytes, path));
                }

                groupNumber++;
            }

            DuplicatesSummaryText = groups.Count == 0
                ? "Žádné duplicitní soubory nenalezeny."
                : $"{groups.Count} skupin duplicit, možná úspora {ByteSizeFormatter.Format(totalReclaimable)}.";
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            DuplicatesSummaryText = $"Hledání duplicit se nepodařilo dokončit: {ex.Message}";
        }
        finally
        {
            HasSearchedForDuplicates = true;
            OnPropertyChanged(nameof(HasDuplicateFiles));
            OnPropertyChanged(nameof(HasSearchedForDuplicates));
            DuplicatesStatusText = string.Empty;
            IsFindingDuplicates = false;
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

        UpdateCompositionSegments(current);
        UpdateTreemapCells(current);

        OnPropertyChanged(nameof(CanNavigateUp));
    }

    /// <summary>
    /// Na rozdíl od <see cref="UpdateCompositionSegments"/> (top 5 + "Ostatní" pro čitelný
    /// pruh) treemapa zvládne zobrazit desítky položek najednou čitelně - žádný strop na
    /// počet, celý seznam Items (+ souhrn přímých souborů, pokud nějaké jsou).
    /// </summary>
    private void UpdateTreemapCells(DirectoryUsageNode current)
    {
        TreemapCells.Clear();

        if (current.SizeBytes <= 0)
        {
            return;
        }

        foreach (var row in Items)
        {
            TreemapCells.Add(new TreemapCellViewModel
            {
                Name = row.Name,
                SizeBytes = row.Node.SizeBytes,
                SizeDisplay = row.SizeDisplay,
                PercentOfParent = row.PercentOfParent,
                Row = row,
            });
        }

        var directFilesSize = current.SizeBytes - current.Subdirectories.Sum(s => s.SizeBytes);
        if (directFilesSize > 0)
        {
            TreemapCells.Add(new TreemapCellViewModel
            {
                Name = "Soubory v této složce",
                SizeBytes = directFilesSize,
                SizeDisplay = ByteSizeFormatter.Format(directFilesSize),
                PercentOfParent = directFilesSize * 100.0 / current.SizeBytes,
                Row = null,
            });
        }
    }

    /// <summary>
    /// Připraví nejvýše 5 pojmenovaných podílů (souboru dat) + volitelnou
    /// souhrnnou položku "Ostatní" pro zbytek - viz skill dataviz, "series-count
    /// ladder": nad 5-6 položek se skládá do jedné souhrnné, negeneruje se
    /// další barva. Přímé soubory v aktuální složce (nejsou samostatnou
    /// podsložkou) se dopočítají jako zbytek celkové velikosti.
    /// </summary>
    private void UpdateCompositionSegments(DirectoryUsageNode current)
    {
        CompositionSegments.Clear();

        if (current.SizeBytes <= 0)
        {
            return;
        }

        var parts = current.Subdirectories
            .Select(s => (s.Name, s.SizeBytes))
            .ToList();

        var directFilesSize = current.SizeBytes - current.Subdirectories.Sum(s => s.SizeBytes);
        if (directFilesSize > 0)
        {
            parts.Add(("Soubory v této složce", directFilesSize));
        }

        var ordered = parts.OrderByDescending(p => p.SizeBytes).ToList();
        const int maxSlots = 5;

        for (var i = 0; i < ordered.Count && i < maxSlots; i++)
        {
            var (name, size) = ordered[i];
            CompositionSegments.Add(new CompositionSegmentViewModel
            {
                Name = name,
                Percent = size * 100.0 / current.SizeBytes,
                SizeDisplay = ByteSizeFormatter.Format(size),
                SeriesIndex = i,
            });
        }

        if (ordered.Count > maxSlots)
        {
            var otherSize = ordered.Skip(maxSlots).Sum(p => p.SizeBytes);
            CompositionSegments.Add(new CompositionSegmentViewModel
            {
                Name = "Ostatní",
                Percent = otherSize * 100.0 / current.SizeBytes,
                SizeDisplay = ByteSizeFormatter.Format(otherSize),
                SeriesIndex = -1,
            });
        }
    }
}

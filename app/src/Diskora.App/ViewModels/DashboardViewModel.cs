using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Diskora.App.Commands;
using Diskora.App.Theming;
using Diskora.Core.Services;
using Diskora.Native;

namespace Diskora.App.ViewModels;

public sealed class DashboardViewModel : ViewModelBase
{
    private readonly IDiskEnumerationService _diskEnumerationService;
    private readonly ThemeService _themeService;
    private string? _errorMessage;
    private bool _isLoading;

    public DashboardViewModel(IDiskEnumerationService diskEnumerationService, ThemeService themeService)
    {
        _diskEnumerationService = diskEnumerationService;
        _themeService = themeService;

        RefreshCommand = new RelayCommand(Refresh, () => !IsLoading);
        ExitCommand = new RelayCommand(() => Application.Current.Shutdown());
        SetLightThemeCommand = new RelayCommand(() => SetTheme(AppTheme.Light));
        SetDarkThemeCommand = new RelayCommand(() => SetTheme(AppTheme.Dark));
        SetSystemThemeCommand = new RelayCommand(() => SetTheme(AppTheme.System));

        IsElevated = ElevationHelper.IsRunningAsAdministrator();
        ElevationStatusText = IsElevated
            ? "Spuštěno s právy administrátora"
            : "Bez práv administrátora — opravy, TRIM/defrag a S.M.A.R.T. budou vyžadovat zvýšení oprávnění";

        Refresh();
    }

    public ObservableCollection<PhysicalDiskRowViewModel> PhysicalDisks { get; } = [];

    public ObservableCollection<VolumeRowViewModel> Volumes { get; } = [];

    public bool IsElevated { get; }

    public string ElevationStatusText { get; }

    public ICommand RefreshCommand { get; }

    public ICommand ExitCommand { get; }

    public ICommand SetLightThemeCommand { get; }

    public ICommand SetDarkThemeCommand { get; }

    public ICommand SetSystemThemeCommand { get; }

    public bool IsLightThemeActive => _themeService.Current == AppTheme.Light;

    public bool IsDarkThemeActive => _themeService.Current == AppTheme.Dark;

    public bool IsSystemThemeActive => _themeService.Current == AppTheme.System;

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetField(ref _isLoading, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetField(ref _errorMessage, value);
    }

    private void SetTheme(AppTheme theme)
    {
        _themeService.Apply(theme);
        OnPropertyChanged(nameof(IsLightThemeActive));
        OnPropertyChanged(nameof(IsDarkThemeActive));
        OnPropertyChanged(nameof(IsSystemThemeActive));
    }

    private void Refresh()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var physicalDisks = _diskEnumerationService.GetPhysicalDisks();

            PhysicalDisks.Clear();
            foreach (var disk in physicalDisks)
            {
                PhysicalDisks.Add(new PhysicalDiskRowViewModel(disk));
            }

            Volumes.Clear();
            foreach (var volume in _diskEnumerationService.GetVolumes())
            {
                var physicalDisk = volume.PhysicalDiskIndex is int diskIndex
                    ? physicalDisks.FirstOrDefault(d => d.Index == diskIndex)
                    : null;
                Volumes.Add(new VolumeRowViewModel(volume, physicalDisk));
            }
        }
        catch (Exception ex) when (ex is System.Management.ManagementException or UnauthorizedAccessException)
        {
            ErrorMessage = $"Disky se nepodařilo načíst: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}

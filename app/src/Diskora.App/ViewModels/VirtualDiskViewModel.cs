using System.Windows.Input;
using Diskora.App.Commands;
using Diskora.Core.Formatting;
using Diskora.Core.Models;
using Diskora.Core.Services;

namespace Diskora.App.ViewModels;

public sealed class VirtualDiskViewModel : ViewModelBase
{
    private readonly IVirtualDiskService _service;
    private bool _isBusy;
    private bool _hasInfo;
    private VirtualDiskFormat _format = VirtualDiskFormat.Unknown;
    private string? _errorMessage;
    private string? _operationMessage;
    private string _formatDisplay = string.Empty;
    private string _virtualSizeDisplay = string.Empty;
    private string _physicalSizeDisplay = string.Empty;
    private string _blockSizeDisplay = string.Empty;
    private string _sectorSizeDisplay = string.Empty;
    private string? _partitionSchemeDisplay;
    private string? _partitionCountDisplay;

    public VirtualDiskViewModel(IVirtualDiskService service, string path)
    {
        _service = service;
        Path = path;

        RefreshCommand = new RelayCommand(RefreshInfo, () => !IsBusy);
        AttachReadOnlyCommand = new RelayCommand(() => Attach(readOnly: true), () => !IsBusy);
        AttachReadWriteCommand = new RelayCommand(() => Attach(readOnly: false), () => !IsBusy);
        DetachCommand = new RelayCommand(Detach, () => !IsBusy);
        MountIsoCommand = new RelayCommand(async () => await MountIsoAsync(), () => !IsBusy);
        DismountIsoCommand = new RelayCommand(async () => await DismountIsoAsync(), () => !IsBusy);
        InspectRawImageCommand = new RelayCommand(InspectRawImage, () => !IsBusy);

        RefreshInfo();
    }

    public string Path { get; }

    public ICommand RefreshCommand { get; }

    public ICommand AttachReadOnlyCommand { get; }

    public ICommand AttachReadWriteCommand { get; }

    public ICommand DetachCommand { get; }

    public ICommand MountIsoCommand { get; }

    public ICommand DismountIsoCommand { get; }

    public ICommand InspectRawImageCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    public bool HasInfo
    {
        get => _hasInfo;
        private set => SetField(ref _hasInfo, value);
    }

    /// <summary>ISO se připojuje jinak než VHD/VHDX (Mount-DiskImage, ne přímé virtdisk.dll) - viz UI.</summary>
    public bool IsIso => _format == VirtualDiskFormat.Iso;

    public bool IsVhdOrVhdx => _format is VirtualDiskFormat.Vhd or VirtualDiskFormat.Vhdx;

    /// <summary>IMG/raw se nedá mountovat (Windows to nepodporuje) - nabízí se jen read-only inspekce rozvržení.</summary>
    public bool IsImg => _format == VirtualDiskFormat.Img;

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetField(ref _errorMessage, value);
    }

    public string? OperationMessage
    {
        get => _operationMessage;
        private set => SetField(ref _operationMessage, value);
    }

    public string FormatDisplay
    {
        get => _formatDisplay;
        private set => SetField(ref _formatDisplay, value);
    }

    public string VirtualSizeDisplay
    {
        get => _virtualSizeDisplay;
        private set => SetField(ref _virtualSizeDisplay, value);
    }

    public string PhysicalSizeDisplay
    {
        get => _physicalSizeDisplay;
        private set => SetField(ref _physicalSizeDisplay, value);
    }

    public string BlockSizeDisplay
    {
        get => _blockSizeDisplay;
        private set => SetField(ref _blockSizeDisplay, value);
    }

    public string SectorSizeDisplay
    {
        get => _sectorSizeDisplay;
        private set => SetField(ref _sectorSizeDisplay, value);
    }

    public string? PartitionSchemeDisplay
    {
        get => _partitionSchemeDisplay;
        private set => SetField(ref _partitionSchemeDisplay, value);
    }

    public string? PartitionCountDisplay
    {
        get => _partitionCountDisplay;
        private set => SetField(ref _partitionCountDisplay, value);
    }

    private void RefreshInfo()
    {
        IsBusy = true;
        ErrorMessage = null;

        var outcome = _service.ReadInfo(Path);
        if (outcome is { Success: true, Summary: { } summary })
        {
            _format = summary.Format;
            FormatDisplay = summary.Format switch
            {
                VirtualDiskFormat.Vhd => "VHD",
                VirtualDiskFormat.Vhdx => "VHDX",
                VirtualDiskFormat.Iso => "ISO",
                VirtualDiskFormat.Img => "IMG/raw",
                _ => "Neznámý formát",
            };
            VirtualSizeDisplay = ByteSizeFormatter.Format((long)summary.VirtualSizeBytes);
            PhysicalSizeDisplay = ByteSizeFormatter.Format((long)summary.PhysicalSizeBytes);
            BlockSizeDisplay = ByteSizeFormatter.Format(summary.BlockSizeBytes);
            SectorSizeDisplay = $"{summary.SectorSizeBytes} B";
            HasInfo = true;
        }
        else
        {
            _format = VirtualDiskFormat.Unknown;
            ErrorMessage = outcome.FailureReason;
            HasInfo = false;
        }

        PartitionSchemeDisplay = null;
        PartitionCountDisplay = null;
        OnPropertyChanged(nameof(IsIso));
        OnPropertyChanged(nameof(IsVhdOrVhdx));
        OnPropertyChanged(nameof(IsImg));
        IsBusy = false;
    }

    private void Attach(bool readOnly)
    {
        IsBusy = true;
        OperationMessage = null;

        var result = _service.Attach(Path, readOnly);
        OperationMessage = result.Success
            ? "Disk byl připojen. Novou jednotku najdete v Průzkumníku (písmeno přidělí Windows automaticky)."
            : result.FailureReason;

        IsBusy = false;
    }

    private void Detach()
    {
        IsBusy = true;
        OperationMessage = null;

        var result = _service.Detach(Path);
        OperationMessage = result.Success ? "Disk byl odpojen." : result.FailureReason;

        IsBusy = false;
    }

    private async Task MountIsoAsync()
    {
        IsBusy = true;
        OperationMessage = null;

        var result = await _service.MountIsoAsync(Path);
        OperationMessage = result.Success
            ? $"Obraz byl připojen jako jednotka {result.DriveLetter}:."
            : result.FailureReason;

        IsBusy = false;
    }

    private async Task DismountIsoAsync()
    {
        IsBusy = true;
        OperationMessage = null;

        var result = await _service.DismountIsoAsync(Path);
        OperationMessage = result.Success ? "Obraz byl odpojen." : result.FailureReason;

        IsBusy = false;
    }

    private void InspectRawImage()
    {
        IsBusy = true;
        OperationMessage = null;

        var result = _service.InspectRawImage(Path);
        if (!result.Success)
        {
            OperationMessage = result.FailureReason;
            PartitionSchemeDisplay = null;
            PartitionCountDisplay = null;
        }
        else
        {
            PartitionSchemeDisplay = result.Scheme switch
            {
                RawImagePartitionScheme.Mbr => "MBR",
                RawImagePartitionScheme.Gpt => "GPT",
                _ => "Nerozpoznáno",
            };
            PartitionCountDisplay = result.Scheme == RawImagePartitionScheme.Unknown
                ? "—"
                : result.PartitionCount.ToString();
        }

        IsBusy = false;
    }
}

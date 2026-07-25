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
    private string? _errorMessage;
    private string? _operationMessage;
    private string _formatDisplay = string.Empty;
    private string _virtualSizeDisplay = string.Empty;
    private string _physicalSizeDisplay = string.Empty;
    private string _blockSizeDisplay = string.Empty;
    private string _sectorSizeDisplay = string.Empty;

    public VirtualDiskViewModel(IVirtualDiskService service, string path)
    {
        _service = service;
        Path = path;

        RefreshCommand = new RelayCommand(RefreshInfo, () => !IsBusy);
        AttachReadOnlyCommand = new RelayCommand(() => Attach(readOnly: true), () => !IsBusy);
        AttachReadWriteCommand = new RelayCommand(() => Attach(readOnly: false), () => !IsBusy);
        DetachCommand = new RelayCommand(Detach, () => !IsBusy);

        RefreshInfo();
    }

    public string Path { get; }

    public ICommand RefreshCommand { get; }

    public ICommand AttachReadOnlyCommand { get; }

    public ICommand AttachReadWriteCommand { get; }

    public ICommand DetachCommand { get; }

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

    private void RefreshInfo()
    {
        IsBusy = true;
        ErrorMessage = null;

        var outcome = _service.ReadInfo(Path);
        if (outcome is { Success: true, Summary: { } summary })
        {
            FormatDisplay = summary.Format switch
            {
                VirtualDiskFormat.Vhd => "VHD",
                VirtualDiskFormat.Vhdx => "VHDX",
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
            ErrorMessage = outcome.FailureReason;
            HasInfo = false;
        }

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
}

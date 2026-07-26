using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Diskora.Native.Smart;

/// <summary>
/// Čte S.M.A.R.T. atributy přes IOCTL_ATA_PASS_THROUGH. Na rozdíl od
/// <see cref="LegacySmartIoctlReader"/> to není obálka nad konkrétní dvojicí
/// SMART příkazů, ale obecný kanál pro libovolný ATA příkaz - proto ho
/// podporuje víc řadičů (mimo jiné některé AHCI a SATA mosty, kde legacy
/// IOCTL_SMART_RCV_DRIVE_DATA vrací jen obecnou chybu).
/// Vyžaduje práva administrátora (viz <see cref="DiskHandle.OpenForSmart"/>).
/// </summary>
public static class AtaPassThroughSmartReader
{
    private const uint IoctlAtaPassThrough = 0x0004D02C;

    private const ushort AtaFlagsDrdyRequired = 0x01;
    private const ushort AtaFlagsDataIn = 0x02;

    /// <summary>
    /// sizeof(ATA_PASS_THROUGH_EX) na x64: Length(2) + AtaFlags(2) + PathId(1) +
    /// TargetId(1) + Lun(1) + ReservedAsUchar(1) + DataTransferLength(4) +
    /// TimeOutValue(4) + ReservedAsUlong(4) + 4 bajty zarovnání před ULONG_PTR +
    /// DataBufferOffset(8) + PreviousTaskFile(8) + CurrentTaskFile(8).
    /// </summary>
    private const int PassThroughStructSize = 48;

    private const int DataBufferOffsetField = 24;
    private const int CurrentTaskFileOffset = 40;

    private const uint TimeoutSeconds = 10;

    public static NativeSmartReadResult Read(int physicalDriveIndex)
    {
        using var handle = DiskHandle.OpenForSmart(physicalDriveIndex, out var openError);

        if (handle is null || handle.IsInvalid)
        {
            return new NativeSmartReadResult(false, openError, []);
        }

        if (!TrySendSmartCommand(handle, AtaSmartCommands.ReadAttributeValues, out var valuesData, out var error))
        {
            return new NativeSmartReadResult(false, error, []);
        }

        // Stejně jako u legacy cesty jsou prahy best-effort - příkaz 0xD1 je
        // v novějších revizích ATA zastaralý a disk ho smí odmítnout.
        var thresholds = TrySendSmartCommand(handle, AtaSmartCommands.ReadThresholds, out var thresholdsData, out _)
            ? SmartAttributeTableParser.ParseThresholds(thresholdsData)
            : null;

        return new NativeSmartReadResult(true, null, SmartAttributeTableParser.ParseAttributes(valuesData, thresholds));
    }

    private static bool TrySendSmartCommand(SafeFileHandle handle, byte featuresRegister, out byte[] data, out string? error)
    {
        // ATA_PASS_THROUGH_EX (bufferovaná varianta) očekává datový buffer ve
        // stejné alokaci hned za strukturou, adresovaný přes DataBufferOffset.
        var buffer = new byte[PassThroughStructSize + SmartAttributeTableParser.DataSize];

        BitConverter.GetBytes((ushort)PassThroughStructSize).CopyTo(buffer, 0);                    // Length
        BitConverter.GetBytes((ushort)(AtaFlagsDrdyRequired | AtaFlagsDataIn)).CopyTo(buffer, 2);  // AtaFlags
        BitConverter.GetBytes((uint)SmartAttributeTableParser.DataSize).CopyTo(buffer, 8);         // DataTransferLength
        BitConverter.GetBytes(TimeoutSeconds).CopyTo(buffer, 12);                                  // TimeOutValue
        BitConverter.GetBytes((ulong)PassThroughStructSize).CopyTo(buffer, DataBufferOffsetField); // DataBufferOffset

        var taskFile = CurrentTaskFileOffset;
        buffer[taskFile + 0] = featuresRegister;
        buffer[taskFile + 1] = 1;                                 // SectorCount
        buffer[taskFile + 2] = 1;                                 // SectorNumber (LBA low)
        buffer[taskFile + 3] = AtaSmartCommands.CylinderLow;      // LBA mid
        buffer[taskFile + 4] = AtaSmartCommands.CylinderHigh;     // LBA high
        buffer[taskFile + 5] = AtaSmartCommands.DeviceHead;
        buffer[taskFile + 6] = AtaSmartCommands.SmartCommand;     // Command

        // Vstupní i výstupní buffer je tentýž kus paměti - ovladač do něj zapíše
        // jak vrácené registry, tak přenesená data.
        var ok = NativeMethods.DeviceIoControl(
            handle, IoctlAtaPassThrough,
            buffer, (uint)buffer.Length,
            buffer, (uint)buffer.Length,
            out _, IntPtr.Zero);

        if (!ok)
        {
            data = [];
            error = $"Zařízení odmítlo ATA pass-through příkaz (Win32 chyba {Marshal.GetLastWin32Error()}).";
            return false;
        }

        data = buffer[PassThroughStructSize..];
        error = null;
        return true;
    }
}

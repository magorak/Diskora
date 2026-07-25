using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Diskora.Native.Smart;

/// <summary>
/// Čte S.M.A.R.T. atributy přes legacy ATA passthrough IOCTL
/// (IOCTL_SMART_RCV_DRIVE_DATA, stejné API jako historicky používají
/// smartmontools, HD Sentinel a další). Funguje spolehlivě pro přímo
/// připojené ATA/SATA disky. Přes USB mosty, hardwarové RAID řadiče a
/// u NVMe disků typicky selže - to je očekávané omezení starého API, ne
/// chyba; volající to musí zobrazit jako "SMART nedostupné", ne pád.
/// Vyžaduje práva administrátora.
/// </summary>
public static class AtaSmartReader
{
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x1;
    private const uint FileShareWrite = 0x2;
    private const uint OpenExisting = 3;

    private const uint IoctlSmartRcvDriveData = 0x0007C088;

    private const byte SmartCommand = 0xB0;
    private const byte SmartReadAttributeValues = 0xD0;
    private const byte SmartReadThresholds = 0xD1;
    private const byte SmartCylLow = 0x4F;
    private const byte SmartCylHigh = 0xC2;

    private const int SendCmdInParamsHeaderSize = 32;
    private const int SendCmdOutParamsHeaderSize = 8;
    private const int SmartDataSize = 512;
    private const int AttributeTableOffset = 2;
    private const int AttributeEntrySize = 12;
    private const int AttributeEntryCount = 30;

    public static NativeSmartReadResult Read(int physicalDriveIndex)
    {
        using var handle = CreateFile(
            $@"\\.\PhysicalDrive{physicalDriveIndex}",
            GenericRead | GenericWrite,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            0,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            return Failure($"Disk se nepodařilo otevřít (Win32 chyba {Marshal.GetLastWin32Error()}). Zkuste spustit Diskoru jako administrátor.");
        }

        if (!TrySendReadCommand(handle, SmartReadAttributeValues, out var valuesData, out var error))
        {
            return Failure(error);
        }

        if (!TrySendReadCommand(handle, SmartReadThresholds, out var thresholdsData, out error))
        {
            return Failure(error);
        }

        var thresholds = ParseThresholds(thresholdsData);
        var attributes = ParseAttributes(valuesData, thresholds);

        return new NativeSmartReadResult(true, null, attributes);
    }

    private static bool TrySendReadCommand(SafeFileHandle handle, byte featuresRegister, out byte[] data, out string? error)
    {
        var inBuffer = new byte[SendCmdInParamsHeaderSize];
        BitConverter.GetBytes((uint)SmartDataSize).CopyTo(inBuffer, 0); // cBufferSize
        inBuffer[4] = featuresRegister; // irDriveRegs.bFeaturesReg
        inBuffer[5] = 1;                // irDriveRegs.bSectorCountReg
        inBuffer[6] = 1;                // irDriveRegs.bSectorNumberReg
        inBuffer[7] = SmartCylLow;      // irDriveRegs.bCylLowReg
        inBuffer[8] = SmartCylHigh;     // irDriveRegs.bCylHighReg
        inBuffer[9] = 0xA0;             // irDriveRegs.bDriveHeadReg
        inBuffer[10] = SmartCommand;    // irDriveRegs.bCommandReg
        inBuffer[12] = 0;               // bDriveNumber

        var outBuffer = new byte[SendCmdOutParamsHeaderSize + SmartDataSize];

        var ok = DeviceIoControl(
            handle, IoctlSmartRcvDriveData,
            inBuffer, (uint)inBuffer.Length,
            outBuffer, (uint)outBuffer.Length,
            out _, IntPtr.Zero);

        if (!ok)
        {
            data = [];
            error = $"Zařízení neodpovědělo na S.M.A.R.T. dotaz (Win32 chyba {Marshal.GetLastWin32Error()}). " +
                     "Obvyklé u USB mostů, RAID řadičů a NVMe disků.";
            return false;
        }

        data = new byte[SmartDataSize];
        Array.Copy(outBuffer, SendCmdOutParamsHeaderSize, data, 0, SmartDataSize);
        error = null;
        return true;
    }

    private static Dictionary<byte, byte> ParseThresholds(byte[] data)
    {
        var thresholds = new Dictionary<byte, byte>();
        var offset = AttributeTableOffset;

        for (var i = 0; i < AttributeEntryCount && offset + AttributeEntrySize <= data.Length; i++, offset += AttributeEntrySize)
        {
            var id = data[offset];
            if (id == 0)
            {
                continue;
            }

            thresholds[id] = data[offset + 1];
        }

        return thresholds;
    }

    private static List<NativeSmartAttribute> ParseAttributes(byte[] data, IReadOnlyDictionary<byte, byte> thresholds)
    {
        var attributes = new List<NativeSmartAttribute>();
        var offset = AttributeTableOffset;

        for (var i = 0; i < AttributeEntryCount && offset + AttributeEntrySize <= data.Length; i++, offset += AttributeEntrySize)
        {
            var id = data[offset];
            if (id == 0)
            {
                continue;
            }

            var current = data[offset + 3];
            var worst = data[offset + 4];

            ulong raw = 0;
            for (var b = 0; b < 6; b++)
            {
                raw |= (ulong)data[offset + 5 + b] << (b * 8);
            }

            thresholds.TryGetValue(id, out var threshold);
            attributes.Add(new NativeSmartAttribute(id, current, worst, threshold, raw));
        }

        return attributes;
    }

    private static NativeSmartReadResult Failure(string? reason) => new(false, reason, []);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName, uint dwDesiredAccess, uint dwShareMode,
        IntPtr lpSecurityAttributes, uint dwCreationDisposition,
        uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice, uint dwIoControlCode,
        byte[] lpInBuffer, uint nInBufferSize,
        byte[] lpOutBuffer, uint nOutBufferSize,
        out uint lpBytesReturned, IntPtr lpOverlapped);
}

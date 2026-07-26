using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Diskora.Native.Smart;

/// <summary>
/// Původní ("legacy") cesta k S.M.A.R.T. datům přes IOCTL_SMART_RCV_DRIVE_DATA -
/// stejné API, jaké historicky používají smartmontools, HD Sentinel a další.
/// Funguje u přímo připojených ATA/SATA disků, ale je to obálka nad pevně danou
/// dvojicí příkazů; řadiče, které ji nepodporují, vrací jen obecnou chybu.
/// Novější a lépe podporovaná alternativa je <see cref="AtaPassThroughSmartReader"/>,
/// mezi oběma přepíná <see cref="AtaSmartReader"/>. Vyžaduje práva administrátora.
/// </summary>
public static class LegacySmartIoctlReader
{
    private const uint IoctlSmartRcvDriveData = 0x0007C088;

    /// <summary>SENDCMDINPARAMS před polem bBuffer: cBufferSize(4) + IDEREGS(8) + bDriveNumber(1) + bReserved[3] + dwReserved[4](16).</summary>
    private const int SendCmdInParamsHeaderSize = 32;

    /// <summary>
    /// SENDCMDOUTPARAMS před polem bBuffer: cBufferSize(4) + DRIVERSTATUS(12).
    /// DRIVERSTATUS je bDriverError(1) + bIDEError(1) + bReserved[2] + dwReserved[2](8).
    /// Dřív tu bylo 8 - o 8 bajtů míň, než ovladač vyžaduje, takže volání vždy
    /// skončilo na ERROR_INSUFFICIENT_BUFFER (122) a legacy cesta nefungovala
    /// na žádném disku (odhaleno až živým testem s admin právy, viz CHANGELOG).
    /// </summary>
    private const int SendCmdOutParamsHeaderSize = 16;

    public static NativeSmartReadResult Read(int physicalDriveIndex)
    {
        using var handle = DiskHandle.OpenForSmart(physicalDriveIndex, out var openError);

        if (handle is null || handle.IsInvalid)
        {
            return new NativeSmartReadResult(false, openError, []);
        }

        if (!TrySendReadCommand(handle, AtaSmartCommands.ReadAttributeValues, out var valuesData, out var error))
        {
            return new NativeSmartReadResult(false, error, []);
        }

        // Prahy jsou best-effort: příkaz 0xD1 je v novějších revizích ATA
        // zastaralý a některé disky ho odmítnou, i když atributy vrátí v pořádku.
        var thresholds = TrySendReadCommand(handle, AtaSmartCommands.ReadThresholds, out var thresholdsData, out _)
            ? SmartAttributeTableParser.ParseThresholds(thresholdsData)
            : null;

        return new NativeSmartReadResult(true, null, SmartAttributeTableParser.ParseAttributes(valuesData, thresholds));
    }

    private static bool TrySendReadCommand(SafeFileHandle handle, byte featuresRegister, out byte[] data, out string? error)
    {
        var inBuffer = new byte[SendCmdInParamsHeaderSize];
        BitConverter.GetBytes((uint)SmartAttributeTableParser.DataSize).CopyTo(inBuffer, 0); // cBufferSize
        inBuffer[4] = featuresRegister;                    // irDriveRegs.bFeaturesReg
        inBuffer[5] = 1;                                   // irDriveRegs.bSectorCountReg
        inBuffer[6] = 1;                                   // irDriveRegs.bSectorNumberReg
        inBuffer[7] = AtaSmartCommands.CylinderLow;        // irDriveRegs.bCylLowReg
        inBuffer[8] = AtaSmartCommands.CylinderHigh;       // irDriveRegs.bCylHighReg
        inBuffer[9] = AtaSmartCommands.DeviceHead;         // irDriveRegs.bDriveHeadReg
        inBuffer[10] = AtaSmartCommands.SmartCommand;      // irDriveRegs.bCommandReg
        inBuffer[12] = 0;                                  // bDriveNumber

        var outBuffer = new byte[SendCmdOutParamsHeaderSize + SmartAttributeTableParser.DataSize];

        var ok = NativeMethods.DeviceIoControl(
            handle, IoctlSmartRcvDriveData,
            inBuffer, (uint)inBuffer.Length,
            outBuffer, (uint)outBuffer.Length,
            out _, IntPtr.Zero);

        if (!ok)
        {
            data = [];
            error = $"Zařízení neodpovědělo na S.M.A.R.T. dotaz (Win32 chyba {Marshal.GetLastWin32Error()}). " +
                    "Obvyklé u USB mostů a RAID řadičů.";
            return false;
        }

        data = outBuffer[SendCmdOutParamsHeaderSize..];
        error = null;
        return true;
    }
}

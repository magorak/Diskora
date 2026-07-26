using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Diskora.Native.Smart;

/// <summary>
/// Čte NVMe log stránku 0x02 ("SMART / Health Information") přes
/// IOCTL_STORAGE_QUERY_PROPERTY s StorageDeviceProtocolSpecificProperty.
/// To je dokumentovaná cesta Windows k NVMe telemetrii - na rozdíl od
/// <see cref="AtaSmartReader"/> nejde o ATA passthrough, takže tenhle reader
/// funguje právě tam, kde legacy IOCTL_SMART_RCV_DRIVE_DATA u NVMe disků selhává.
/// Handle se otevírá bez GENERIC_READ/GENERIC_WRITE (dwDesiredAccess = 0) -
/// dotaz na vlastnost zařízení nepotřebuje práva ke čtení dat z disku.
/// </summary>
public static class NvmeHealthReader
{
    private const uint FileShareRead = 0x1;
    private const uint FileShareWrite = 0x2;
    private const uint OpenExisting = 3;

    private const uint IoctlStorageQueryProperty = 0x002D1400;

    private const uint StorageDeviceProtocolSpecificProperty = 50;
    private const uint PropertyStandardQuery = 0;

    private const uint ProtocolTypeNvme = 3;
    private const uint NvmeDataTypeLogPage = 2;
    private const uint NvmeLogPageHealthInfo = 0x02;

    // STORAGE_PROPERTY_QUERY: PropertyId(4) + QueryType(4), pak následují AdditionalParameters.
    private const int PropertyQueryHeaderSize = 8;

    // STORAGE_PROTOCOL_SPECIFIC_DATA: 10 × DWORD.
    private const int ProtocolSpecificDataSize = 40;

    // STORAGE_PROTOCOL_DATA_DESCRIPTOR: Version(4) + Size(4) + STORAGE_PROTOCOL_SPECIFIC_DATA.
    private const int ProtocolDataDescriptorHeaderSize = 8 + ProtocolSpecificDataSize;

    private const int HealthLogSize = 512;

    public static NativeNvmeHealthResult Read(int physicalDriveIndex)
    {
        using var handle = CreateFile(
            $@"\\.\PhysicalDrive{physicalDriveIndex}",
            0,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            0,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            return Failure($"Disk se nepodařilo otevřít (Win32 chyba {Marshal.GetLastWin32Error()}).");
        }

        var input = BuildQuery();
        var output = new byte[ProtocolDataDescriptorHeaderSize + HealthLogSize];

        var ok = DeviceIoControl(
            handle, IoctlStorageQueryProperty,
            input, (uint)input.Length,
            output, (uint)output.Length,
            out var bytesReturned, IntPtr.Zero);

        if (!ok)
        {
            return Failure($"Zařízení neodpovědělo na dotaz na NVMe health log (Win32 chyba {Marshal.GetLastWin32Error()}). " +
                           "Očekávané u disků, které nejsou NVMe.");
        }

        // ProtocolDataOffset je relativní ke ZAČÁTKU STORAGE_PROTOCOL_SPECIFIC_DATA,
        // tj. k offsetu 8 ve vráceném deskriptoru - ne k začátku bufferu.
        var dataOffset = 8 + (int)BitConverter.ToUInt32(output, 8 + 16);
        var dataLength = (int)BitConverter.ToUInt32(output, 8 + 20);

        if (dataLength < HealthLogSize || dataOffset < 0 || dataOffset + HealthLogSize > bytesReturned || dataOffset + HealthLogSize > output.Length)
        {
            return Failure("Řadič vrátil NVMe health log v nečekaném tvaru (neúplná data).");
        }

        return new NativeNvmeHealthResult(true, null, Parse(output.AsSpan(dataOffset, HealthLogSize)));
    }

    private static byte[] BuildQuery()
    {
        var query = new byte[PropertyQueryHeaderSize + ProtocolSpecificDataSize];

        BitConverter.GetBytes(StorageDeviceProtocolSpecificProperty).CopyTo(query, 0);
        BitConverter.GetBytes(PropertyStandardQuery).CopyTo(query, 4);

        var p = PropertyQueryHeaderSize;
        BitConverter.GetBytes(ProtocolTypeNvme).CopyTo(query, p + 0);       // ProtocolType
        BitConverter.GetBytes(NvmeDataTypeLogPage).CopyTo(query, p + 4);    // DataType
        BitConverter.GetBytes(NvmeLogPageHealthInfo).CopyTo(query, p + 8);  // ProtocolDataRequestValue
        BitConverter.GetBytes(0u).CopyTo(query, p + 12);                    // ProtocolDataRequestSubValue
        BitConverter.GetBytes((uint)ProtocolSpecificDataSize).CopyTo(query, p + 16); // ProtocolDataOffset
        BitConverter.GetBytes((uint)HealthLogSize).CopyTo(query, p + 20);   // ProtocolDataLength

        return query;
    }

    /// <summary>
    /// Rozloží 512bajtovou log stránku dle NVMe Base Specification (Figure "SMART /
    /// Health Information Log Page"). Čítače jsou ve specifikaci 128bitové; čte se
    /// jen spodních 64 bitů - horní polovina by přetekla až u hodnot, kterých reálné
    /// disky nedosáhnou (2^64 datových jednotek je řádově 9 × 10^12 TB).
    /// </summary>
    private static NativeNvmeHealthLog Parse(ReadOnlySpan<byte> log) => new(
        CriticalWarning: log[0],
        CompositeTemperatureKelvin: BitConverter.ToUInt16(log[1..3]),
        AvailableSparePercent: log[3],
        AvailableSpareThresholdPercent: log[4],
        PercentageUsed: log[5],
        DataUnitsRead: BitConverter.ToUInt64(log[32..40]),
        DataUnitsWritten: BitConverter.ToUInt64(log[48..56]),
        PowerCycles: BitConverter.ToUInt64(log[112..120]),
        PowerOnHours: BitConverter.ToUInt64(log[128..136]),
        UnsafeShutdowns: BitConverter.ToUInt64(log[144..152]),
        MediaErrors: BitConverter.ToUInt64(log[160..168]),
        ErrorLogEntryCount: BitConverter.ToUInt64(log[176..184]));

    private static NativeNvmeHealthResult Failure(string reason) => new(false, reason, null);

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

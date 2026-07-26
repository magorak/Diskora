using Diskora.Core.Models;
using Diskora.Core.Smart;
using Diskora.Native.Smart;

namespace Diskora.Core.Services;

public sealed class SmartService(IDiskHistoryStore? historyStore = null) : ISmartService
{
    public SmartReadResult ReadReport(int physicalDiskIndex)
    {
        // NVMe se zkouší první: dotaz na log stránku uspěje jen u skutečně NVMe
        // zařízení, je levný a nepotřebuje práva administrátora. Teprve když
        // neuspěje, jde se na ATA passthrough, který elevaci vyžaduje vždy.
        var nvmeResult = TryReadNvme(physicalDiskIndex);
        if (nvmeResult is not null)
        {
            return nvmeResult;
        }

        NativeSmartReadResult nativeResult;
        try
        {
            nativeResult = AtaSmartReader.Read(physicalDiskIndex);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return new SmartReadResult(false, $"S.M.A.R.T. data se nepodařilo přečíst: {ex.Message}", null);
        }

        if (!nativeResult.Success)
        {
            return new SmartReadResult(false, nativeResult.FailureReason, null);
        }

        var readings = nativeResult.Attributes
            .Select(a => new SmartAttributeReading(a.Id, a.CurrentValue, a.WorstValue, a.Threshold, a.RawValue))
            .ToList();

        return Success(new SmartReport(
            physicalDiskIndex,
            DateTimeOffset.UtcNow,
            readings,
            SmartHealthEvaluator.EvaluateOverallHealth(readings)));
    }

    /// <summary>Vrátí report, jen když jde skutečně o NVMe disk; jinak null (= zkusit ATA cestu).</summary>
    private SmartReadResult? TryReadNvme(int physicalDiskIndex)
    {
        NativeNvmeHealthResult result;
        try
        {
            result = NvmeHealthReader.Read(physicalDiskIndex);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return null;
        }

        if (!result.Success || result.Log is null)
        {
            return null;
        }

        var log = result.Log;
        var info = new NvmeHealthInfo(
            log.CriticalWarning,
            log.CompositeTemperatureKelvin,
            log.AvailableSparePercent,
            log.AvailableSpareThresholdPercent,
            log.PercentageUsed,
            log.DataUnitsRead,
            log.DataUnitsWritten,
            log.PowerCycles,
            log.PowerOnHours,
            log.UnsafeShutdowns,
            log.MediaErrors,
            log.ErrorLogEntryCount);

        return Success(new SmartReport(
            physicalDiskIndex,
            DateTimeOffset.UtcNow,
            [],
            NvmeHealthEvaluator.EvaluateOverallHealth(info),
            info));
    }

    private SmartReadResult Success(SmartReport report)
    {
        historyStore?.RecordSmartReading(report.DiskIndex, report.OverallHealth);
        return new SmartReadResult(true, null, report);
    }
}

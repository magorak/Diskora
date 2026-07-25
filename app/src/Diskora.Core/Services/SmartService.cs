using Diskora.Core.Models;
using Diskora.Core.Smart;
using Diskora.Native.Smart;

namespace Diskora.Core.Services;

public sealed class SmartService(IDiskHistoryStore? historyStore = null) : ISmartService
{
    public SmartReadResult ReadReport(int physicalDiskIndex)
    {
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

        var report = new SmartReport(
            physicalDiskIndex,
            DateTimeOffset.UtcNow,
            readings,
            SmartHealthEvaluator.EvaluateOverallHealth(readings));

        historyStore?.RecordSmartReading(physicalDiskIndex, report.OverallHealth);

        return new SmartReadResult(true, null, report);
    }
}

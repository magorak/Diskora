using Diskora.Core.Models;
using Diskora.Core.Smart;

namespace Diskora.Core.Tests;

public class SmartHealthEvaluatorTests
{
    [Fact]
    public void EvaluateAttributeRisk_CurrentAboveThreshold_IsOk()
    {
        var reading = new SmartAttributeReading(Id: 1, CurrentValue: 100, WorstValue: 100, Threshold: 6, RawValue: 0);

        var risk = SmartHealthEvaluator.EvaluateAttributeRisk(reading);

        Assert.Equal(SmartAttributeRisk.Ok, risk);
    }

    [Fact]
    public void EvaluateAttributeRisk_CurrentAtOrBelowThreshold_IsCritical()
    {
        var reading = new SmartAttributeReading(Id: 1, CurrentValue: 5, WorstValue: 5, Threshold: 6, RawValue: 0);

        var risk = SmartHealthEvaluator.EvaluateAttributeRisk(reading);

        Assert.Equal(SmartAttributeRisk.Critical, risk);
    }

    [Theory]
    [InlineData((byte)5)]
    [InlineData((byte)196)]
    [InlineData((byte)197)]
    [InlineData((byte)198)]
    public void EvaluateAttributeRisk_SectorAttributeWithNonZeroRaw_IsWarning(byte attributeId)
    {
        var reading = new SmartAttributeReading(Id: attributeId, CurrentValue: 100, WorstValue: 100, Threshold: 0, RawValue: 3);

        var risk = SmartHealthEvaluator.EvaluateAttributeRisk(reading);

        Assert.Equal(SmartAttributeRisk.Warning, risk);
    }

    [Fact]
    public void EvaluateAttributeRisk_SectorAttributeWithZeroRaw_IsOk()
    {
        var reading = new SmartAttributeReading(Id: 5, CurrentValue: 100, WorstValue: 100, Threshold: 0, RawValue: 0);

        var risk = SmartHealthEvaluator.EvaluateAttributeRisk(reading);

        Assert.Equal(SmartAttributeRisk.Ok, risk);
    }

    [Fact]
    public void EvaluateOverallHealth_NoReadings_IsUnknown()
    {
        var health = SmartHealthEvaluator.EvaluateOverallHealth([]);

        Assert.Equal(DiskHealthStatus.Unknown, health);
    }

    [Fact]
    public void EvaluateOverallHealth_AllOk_IsHealthy()
    {
        SmartAttributeReading[] readings =
        [
            new(1, 100, 100, 6, 0),
            new(9, 90, 90, 0, 12345),
        ];

        var health = SmartHealthEvaluator.EvaluateOverallHealth(readings);

        Assert.Equal(DiskHealthStatus.Healthy, health);
    }

    [Fact]
    public void EvaluateOverallHealth_OneWarningNoneCritical_IsWarning()
    {
        SmartAttributeReading[] readings =
        [
            new(1, 100, 100, 6, 0),
            new(197, 100, 100, 0, 2),
        ];

        var health = SmartHealthEvaluator.EvaluateOverallHealth(readings);

        Assert.Equal(DiskHealthStatus.Warning, health);
    }

    [Fact]
    public void EvaluateOverallHealth_AnyCritical_IsCriticalEvenWithWarnings()
    {
        SmartAttributeReading[] readings =
        [
            new(197, 100, 100, 0, 2),
            new(5, 3, 3, 10, 1),
        ];

        var health = SmartHealthEvaluator.EvaluateOverallHealth(readings);

        Assert.Equal(DiskHealthStatus.Critical, health);
    }
}

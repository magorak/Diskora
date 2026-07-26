using Diskora.App.Display;
using Diskora.Core.Models;
using Diskora.Core.Smart;

namespace Diskora.App.ViewModels;

public sealed class SmartAttributeRowViewModel(SmartAttributeReading reading)
{
    public byte Id { get; } = reading.Id;

    public string Name { get; } = SmartAttributeCatalog.GetName(reading.Id);

    public string Explanation { get; } = SmartAttributeCatalog.GetExplanation(reading.Id);

    public byte CurrentValue { get; } = reading.CurrentValue;

    public byte WorstValue { get; } = reading.WorstValue;

    public byte Threshold { get; } = reading.Threshold;

    public ulong RawValue { get; } = reading.RawValue;

    public SmartAttributeRisk Risk { get; } = SmartHealthEvaluator.EvaluateAttributeRisk(reading);

    public string RiskDisplay => Risk.ToDisplayText();
}

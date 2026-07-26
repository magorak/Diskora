using Diskora.App.Display;
using Diskora.Core.Models;

namespace Diskora.App.ViewModels;

public sealed class NvmeHealthMetricRowViewModel(NvmeHealthMetric metric)
{
    public string Name { get; } = metric.Name;

    public string Value { get; } = metric.Value;

    public string Explanation { get; } = metric.Explanation;

    public SmartAttributeRisk Risk { get; } = metric.Risk;

    public string RiskDisplay => Risk.ToDisplayText();
}

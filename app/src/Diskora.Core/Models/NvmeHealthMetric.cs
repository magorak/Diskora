namespace Diskora.Core.Models;

/// <summary>
/// Jeden řádek NVMe zdravotního přehledu připravený k zobrazení: co to je,
/// jaká je hodnota, co to znamená a jak je to rizikové. NVMe obdoba dvojice
/// <see cref="SmartAttributeReading"/> + <c>SmartAttributeCatalog</c>.
/// </summary>
public sealed record NvmeHealthMetric(
    string Name,
    string Value,
    string Explanation,
    SmartAttributeRisk Risk);

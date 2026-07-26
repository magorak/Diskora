namespace Diskora.Native.Smart;

/// <summary>Výsledek pokusu o přečtení NVMe health logu. Neúspěch není chyba - u ne-NVMe disku se čeká.</summary>
public sealed record NativeNvmeHealthResult(bool Success, string? FailureReason, NativeNvmeHealthLog? Log);

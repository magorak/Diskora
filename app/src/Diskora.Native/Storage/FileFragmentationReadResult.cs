namespace Diskora.Native.Storage;

/// <summary>
/// Výsledek zjištění počtu fragmentů (nesouvislých rozsahů clusterů) souboru.
/// <see cref="ExtentCountIsLowerBound"/> = soubor má víc fragmentů, než kolik
/// se vešlo do sledovaného bufferu - <see cref="ExtentCount"/> pak je jen
/// dolní odhad ("aspoň tolik"), ne přesné číslo.
/// </summary>
public readonly record struct FileFragmentationReadResult(
    bool Success, string? FailureReason, int ExtentCount, bool ExtentCountIsLowerBound);

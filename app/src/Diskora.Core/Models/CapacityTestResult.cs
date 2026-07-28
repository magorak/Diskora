namespace Diskora.Core.Models;

/// <summary>Ve které části testu se právě je - kvůli srozumitelnému hlášení postupu.</summary>
public enum CapacityTestPhase
{
    Writing,
    Verifying,
    CleaningUp,
}

public sealed record CapacityTestProgress(CapacityTestPhase Phase, long BytesProcessed, long BytesTotal)
{
    public double Percent => BytesTotal <= 0 ? 0 : Math.Clamp(BytesProcessed * 100.0 / BytesTotal, 0, 100);
}

/// <summary>
/// Výsledek testu skutečné kapacity. <paramref name="FirstMismatchOffset"/> je
/// pozice prvního bajtu, který se po zápisu přečetl jinak - u přeznačeného disku
/// zhruba odpovídá jeho skutečné kapacitě.
/// </summary>
public sealed record CapacityTestResult(
    bool Completed,
    string? FailureReason,
    long BytesWritten,
    long BytesVerified,
    long? FirstMismatchOffset)
{
    /// <summary>True jen když se všechno zapsané dalo přečíst zpátky beze změny.</summary>
    public bool DataIsIntact => Completed && FirstMismatchOffset is null && BytesWritten > 0;
}

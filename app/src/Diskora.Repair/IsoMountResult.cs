namespace Diskora.Repair;

public sealed record IsoMountResult(bool Success, string? FailureReason, string? DriveLetter);

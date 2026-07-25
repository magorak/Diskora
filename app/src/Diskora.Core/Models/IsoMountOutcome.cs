namespace Diskora.Core.Models;

public sealed record IsoMountOutcome(bool Success, string? FailureReason, string? DriveLetter);

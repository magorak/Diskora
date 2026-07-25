namespace Diskora.Native.Smart;

public sealed record NativeSmartReadResult(bool Success, string? FailureReason, IReadOnlyList<NativeSmartAttribute> Attributes);

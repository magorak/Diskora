namespace Diskora.Core.Models;

/// <summary>Zjištěné zhoršení zdraví disku mezi dvěma po sobě jdoucími S.M.A.R.T. kontrolami.</summary>
public sealed record DiskHealthChangeResult(int DiskIndex, DiskHealthStatus PreviousHealth, DiskHealthStatus CurrentHealth);

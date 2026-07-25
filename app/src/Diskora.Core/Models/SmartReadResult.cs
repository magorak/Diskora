namespace Diskora.Core.Models;

/// <summary>
/// Výsledek pokusu o čtení S.M.A.R.T. dat. Nedostupnost (USB most, RAID
/// řadič, NVMe bez podpory legacy passthrough, chybějící oprávnění) je
/// očekávaný a běžný stav, ne chyba - proto explicitní IsSupported/Reason
/// místo výjimky.
/// </summary>
public sealed record SmartReadResult(bool IsSupported, string? UnavailableReason, SmartReport? Report);

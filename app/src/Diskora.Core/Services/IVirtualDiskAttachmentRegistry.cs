using Diskora.Core.Models;

namespace Diskora.Core.Services;

/// <summary>
/// Sleduje, které virtuální disky/obrazy Diskora aktuálně drží připojené (VHD/VHDX
/// přes virtdisk.dll, ISO přes Mount-DiskImage). Windows tato připojení drží nezávisle
/// na životnosti procesu Diskory (viz komentář u <c>VirtualDiskAttacher</c> - záměrně
/// kvůli <c>ATTACH_VIRTUAL_DISK_FLAG_PERMANENT_LIFETIME</c>), takže po pádu aplikace
/// nebo zavření bez odpojení zůstane disk připojený i po skončení procesu. Tenhle
/// registr umožňuje při dalším startu upozornit uživatele na to, co zůstalo z
/// minulého běhu připojené, aby na to nezapomněl. Implementace (SQLite) žije v
/// Diskora.Data.
/// </summary>
public interface IVirtualDiskAttachmentRegistry
{
    void RecordAttached(string path, VirtualDiskFormat format, bool readOnly);

    void RecordDetached(string path);

    /// <summary>
    /// Vrací záznamy, jejichž soubor stále existuje - záznamy pro mezitím smazané/
    /// přesunuté soubory se tiše odstraní (nemá smysl na ně upozorňovat).
    /// </summary>
    IReadOnlyList<AttachedVirtualDiskEntry> GetTrackedAttachments();
}

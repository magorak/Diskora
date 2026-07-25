using Diskora.Core.Models;

namespace Diskora.Core.Services;

/// <summary>
/// Lokální historie zdraví disků a výsledků kontrol integrity - odlišující
/// prvek Diskory oproti konkurenci (žádný cloud, čistě lokální trend v čase).
/// Implementace (SQLite) žije v Diskora.Data; Core zná jen toto rozhraní,
/// aby SmartService/IntegrityCheckService mohly zápis historie používat bez
/// závislosti na konkrétním úložišti.
/// </summary>
public interface IDiskHistoryStore
{
    void RecordSmartReading(int diskIndex, DiskHealthStatus overallHealth);

    IReadOnlyList<SmartHistoryEntry> GetRecentSmartHistory(int diskIndex, int maxCount = 20);

    void RecordIntegrityCheck(string driveLetter, VolumeDirtyState dirtyState, int? scanExitCode, bool? scanAppearsClean);

    IReadOnlyList<IntegrityHistoryEntry> GetRecentIntegrityHistory(string driveLetter, int maxCount = 20);
}

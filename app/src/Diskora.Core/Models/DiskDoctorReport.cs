namespace Diskora.Core.Models;

/// <summary>Závažnost jednoho zjištění Disk Doctora. Pořadí je významné - celkový verdikt je maximum.</summary>
public enum DiskDoctorSeverity
{
    /// <summary>Zkontrolováno a v pořádku.</summary>
    Ok,

    /// <summary>Nic špatného, jen informace nebo nabídka údržby.</summary>
    Info,

    /// <summary>Něco si zaslouží pozornost, ale nehoří to.</summary>
    Warning,

    /// <summary>Hrozí ztráta dat, jednejte hned.</summary>
    Critical,
}

/// <summary>
/// Co Diskora doporučuje udělat. Disk Doctor je záměrně jen diagnostický -
/// sám nic nespouští, akce jen nabízí, protože část z nich (spotfix, defragmentace)
/// na disk skutečně zapisuje a patří pod výslovné potvrzení uživatele.
/// </summary>
public enum DiskDoctorAction
{
    None,

    /// <summary>Zálohovat data, dokud je disk čitelný.</summary>
    BackUpNow,

    /// <summary>Spustit needestruktivní kontrolu integrity (`chkdsk /scan`).</summary>
    RunIntegrityScan,

    /// <summary>Spustit opravu souborového systému (`Repair-Volume -SpotFix`) - ZAPISUJE.</summary>
    RunSpotFix,

    /// <summary>Spustit povrchový sken fyzického disku a najít nečitelné oblasti.</summary>
    RunSurfaceScan,

    /// <summary>Spustit TRIM (SSD).</summary>
    RunTrim,

    /// <summary>Spustit defragmentaci (HDD) - ZAPISUJE.</summary>
    RunDefragment,

    /// <summary>Zkontrolovat datový kabel/konektor, ne disk samotný.</summary>
    CheckCable,

    /// <summary>Spustit Diskoru znovu s právy administrátora a kontrolu zopakovat.</summary>
    RunAsAdministrator,
}

/// <summary>Jedno zjištění: co se našlo, co to znamená a co s tím.</summary>
public sealed record DiskDoctorFinding(
    string Title,
    string Detail,
    DiskDoctorSeverity Severity,
    DiskDoctorAction RecommendedAction);

/// <summary>Souhrn kontroly jednoho svazku a fyzického disku pod ním.</summary>
public sealed record DiskDoctorReport(
    string Subject,
    DiskDoctorSeverity Overall,
    IReadOnlyList<DiskDoctorFinding> Findings);

/// <summary>
/// Vstupy rozhodování - všechno, co Disk Doctor potřebuje vědět, posbírané
/// jinde. Díky tomu je samotné rozhodování čistá funkce testovatelná bez disků.
/// </summary>
public sealed record DiskDoctorInputs(
    SmartReadResult Smart,
    VolumeDirtyState DirtyState,
    DiskOptimizationCapabilities Capabilities,
    bool IsRunningAsAdministrator);

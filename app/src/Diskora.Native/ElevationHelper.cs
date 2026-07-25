using System.Security.Principal;

namespace Diskora.Native;

/// <summary>
/// Zjišťuje, zda proces běží se zvýšenými (administrátorskými) právy.
/// Diskora se spouští s právy "asInvoker" (viz app.manifest) a operace
/// vyžadující elevaci (oprava disku, TRIM/defrag, SMART přes IOCTL) si ji
/// vyžádají zvlášť — viz docs/SECURITY.md, princip nejnižších nutných práv.
/// </summary>
public static class ElevationHelper
{
    public static bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}

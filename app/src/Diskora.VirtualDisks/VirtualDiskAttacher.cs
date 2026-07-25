using System.Runtime.InteropServices;
using Diskora.VirtualDisks.Interop;

namespace Diskora.VirtualDisks;

/// <summary>
/// Připojuje/odpojuje VHD/VHDX jako svazek se souborovým systémem. Na rozdíl
/// od <see cref="VirtualDiskReader"/> vyžaduje práva administrátora - to je
/// očekávané omezení Windows (stejné jako u Mount-VHD), ne chyba Diskory.
/// Handle se po úspěšném i neúspěšném volání vždy zavře - připojení VHD
/// je stav sledovaný systémem nezávisle na životnosti handle (stejně jako
/// u Mount-VHD/Dismount-VHD).
/// </summary>
public static class VirtualDiskAttacher
{
    private const int VirtualDiskAccessAttachRo = 0x00010000;
    private const int VirtualDiskAccessAttachRw = 0x00020000;
    private const int VirtualDiskAccessDetach = 0x00040000;
    private const int VirtualDiskAccessGetInfo = 0x00080000;
    private const int AttachVirtualDiskFlagReadOnly = 0x00000001;

    public static VirtualDiskAttachResult Attach(string path, bool readOnly)
    {
        var accessMask = (readOnly ? VirtualDiskAccessAttachRo : VirtualDiskAccessAttachRw) | VirtualDiskAccessGetInfo;

        return WithOpenHandle(path, accessMask, handle =>
        {
            var attachParams = VirtDiskNativeMethods.BuildAttachParametersV1();
            var attachParamsHandle = GCHandle.Alloc(attachParams, GCHandleType.Pinned);
            try
            {
                var flags = readOnly ? AttachVirtualDiskFlagReadOnly : 0;
                var result = VirtDiskNativeMethods.AttachVirtualDisk(
                    handle, IntPtr.Zero, flags, 0, attachParamsHandle.AddrOfPinnedObject(), IntPtr.Zero);

                return result == 0
                    ? new VirtualDiskAttachResult(true, null)
                    : new VirtualDiskAttachResult(false, DescribeFailure("připojit", result));
            }
            finally
            {
                attachParamsHandle.Free();
            }
        });
    }

    public static VirtualDiskAttachResult Detach(string path)
    {
        return WithOpenHandle(path, VirtualDiskAccessDetach, handle =>
        {
            var result = VirtDiskNativeMethods.DetachVirtualDisk(handle, 0, 0);
            return result == 0
                ? new VirtualDiskAttachResult(true, null)
                : new VirtualDiskAttachResult(false, DescribeFailure("odpojit", result));
        });
    }

    private static VirtualDiskAttachResult WithOpenHandle(
        string path, int accessMask, Func<IntPtr, VirtualDiskAttachResult> action)
    {
        var storageType = default(VirtualStorageType);
        // Verze 1 parametrů - viz komentář u BuildOpenParametersV1: verze 2 s
        // nenulovou přístupovou maskou (potřeba pro attach/detach) empiricky
        // selhávala s ERROR_INVALID_PARAMETER.
        var openParams = VirtDiskNativeMethods.BuildOpenParametersV1();
        var openParamsHandle = GCHandle.Alloc(openParams, GCHandleType.Pinned);

        try
        {
            var openResult = VirtDiskNativeMethods.OpenVirtualDisk(
                ref storageType, path, accessMask, flags: 0,
                openParamsHandle.AddrOfPinnedObject(), out var handle);

            if (openResult != 0)
            {
                return new VirtualDiskAttachResult(false, DescribeFailure("otevřít pro připojení", openResult));
            }

            try
            {
                return action(handle);
            }
            finally
            {
                VirtDiskNativeMethods.CloseHandle(handle);
            }
        }
        finally
        {
            openParamsHandle.Free();
        }
    }

    private const int ErrorAccessDenied = 5;
    private const int ErrorPrivilegeNotHeld = 1314;

    private static string DescribeFailure(string operation, int win32Error)
    {
        var isPrivilegeIssue = win32Error is ErrorAccessDenied or ErrorPrivilegeNotHeld;
        return isPrivilegeIssue
            ? $"Disk se nepodařilo {operation} - chybí práva administrátora (Win32 chyba {win32Error}). Spusťte Diskoru jako administrátor a zkuste to znovu."
            : $"Disk se nepodařilo {operation} (Win32 chyba {win32Error}).";
    }
}

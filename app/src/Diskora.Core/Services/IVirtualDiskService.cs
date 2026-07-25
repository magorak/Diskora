using Diskora.Core.Models;

namespace Diskora.Core.Services;

public interface IVirtualDiskService
{
    VirtualDiskReadOutcome ReadInfo(string path);

    VirtualDiskOperationOutcome Attach(string path, bool readOnly);

    VirtualDiskOperationOutcome Detach(string path);
}

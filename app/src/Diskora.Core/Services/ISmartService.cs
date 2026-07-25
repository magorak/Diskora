using Diskora.Core.Models;

namespace Diskora.Core.Services;

public interface ISmartService
{
    SmartReadResult ReadReport(int physicalDiskIndex);
}

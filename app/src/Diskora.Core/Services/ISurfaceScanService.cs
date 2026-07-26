using Diskora.Core.Models;

namespace Diskora.Core.Services;

public interface ISurfaceScanService
{
    Task<SurfaceScanResult> ScanAsync(
        int physicalDiskIndex,
        long sizeBytes,
        IProgress<double>? percentProgress = null,
        CancellationToken cancellationToken = default);
}

using Diskora.Core.Models;

namespace Diskora.Core.Services;

public interface IDiskUsageScanner
{
    Task<DiskUsageScanResult> ScanAsync(
        string rootPath,
        IProgress<string>? onDirectoryScanned = null,
        CancellationToken cancellationToken = default);
}

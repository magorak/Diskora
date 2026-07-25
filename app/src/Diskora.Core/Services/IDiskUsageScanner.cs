using Diskora.Core.Models;

namespace Diskora.Core.Services;

public interface IDiskUsageScanner
{
    Task<DirectoryUsageNode> ScanAsync(
        string rootPath,
        IProgress<string>? onDirectoryScanned = null,
        CancellationToken cancellationToken = default);
}

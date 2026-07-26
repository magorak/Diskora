using Diskora.Core.Models;

namespace Diskora.Core.Services;

public interface IDuplicateFileFinder
{
    Task<IReadOnlyList<DuplicateFileGroup>> FindAsync(
        string rootPath,
        IProgress<string>? onFileScanned = null,
        CancellationToken cancellationToken = default);
}

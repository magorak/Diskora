using Diskora.Core.Models;
using Diskora.Native.Storage;

namespace Diskora.Core.Services;

public sealed class SurfaceScanService : ISurfaceScanService
{
    public async Task<SurfaceScanResult> ScanAsync(
        int physicalDiskIndex,
        long sizeBytes,
        IProgress<double>? percentProgress = null,
        CancellationToken cancellationToken = default)
    {
        var bytesProgress = percentProgress is null
            ? null
            : new Progress<long>(scanned => percentProgress.Report(sizeBytes == 0 ? 100 : Math.Clamp(scanned * 100.0 / sizeBytes, 0, 100)));

        var nativeResult = await PhysicalDiskSurfaceScanner
            .ScanAsync(physicalDiskIndex, sizeBytes, bytesProgress, cancellationToken)
            .ConfigureAwait(false);

        var badRanges = nativeResult.BadRanges
            .Select(r => new BadSectorRange(r.OffsetBytes, r.LengthBytes))
            .ToList();

        return new SurfaceScanResult(nativeResult.Success, nativeResult.FailureReason, nativeResult.BytesScanned, sizeBytes, badRanges);
    }
}

using Diskora.Core.Models;
using Diskora.Native.Storage;
using Diskora.Repair;

namespace Diskora.Core.Services;

public sealed class DiskOptimizationService(IDiskEnumerationService? diskEnumerationService = null) : IDiskOptimizationService
{
    private readonly IDiskEnumerationService _diskEnumerationService = diskEnumerationService ?? new DiskEnumerationService();

    public DiskOptimizationCapabilities GetCapabilities(string driveLetter)
    {
        var hasSeekPenalty = StoragePropertyReader.HasSeekPenalty(driveLetter);

        // IOCTL je první volba, ale u disků za USB mostem vrací null. Dřív to
        // znamenalo, že Diskora nenabídla vůbec nic - a to i tam, kde Windows
        // svým „Optimalizovat jednotky" pracovat umí (nahlásil uživatel).
        // Druhý názor si proto vyžádáme z WMI přes typ média fyzického disku.
        hasSeekPenalty ??= GetSeekPenaltyFromMediaType(driveLetter);

        return new DiskOptimizationCapabilities(hasSeekPenalty, StoragePropertyReader.SupportsTrim(driveLetter));
    }

    /// <summary>
    /// Odvodí rotačnost z WMI typu média. Vrací null u všeho, co si není jisté -
    /// hlavně u <see cref="DiskMediaType.Unspecified"/>, což hlásí právě USB disky.
    /// Záměrně se nekouká na `SpindleSpeed`: u testovaného USB disku je 0, což by
    /// ho falešně prohlásilo za SSD.
    /// </summary>
    private bool? GetSeekPenaltyFromMediaType(string driveLetter)
    {
        try
        {
            var normalized = driveLetter.TrimEnd('\\', ':');

            var volume = _diskEnumerationService.GetVolumes()
                .FirstOrDefault(v => string.Equals(v.Name.TrimEnd('\\', ':'), normalized, StringComparison.OrdinalIgnoreCase));

            if (volume?.PhysicalDiskIndex is not { } diskIndex)
            {
                return null;
            }

            return _diskEnumerationService.GetPhysicalDisks().FirstOrDefault(d => d.Index == diskIndex)?.MediaType switch
            {
                DiskMediaType.HardDisk => true,
                DiskMediaType.SolidState or DiskMediaType.StorageClassMemory => false,
                _ => null,
            };
        }
        catch (Exception ex) when (ex is System.Management.ManagementException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public async Task<OptimizationRunOutcome> RunTrimAsync(
        string driveLetter, IProgress<string>? onOutputLine = null, CancellationToken cancellationToken = default)
    {
        var result = await DefragRunner.RunTrimAsync(driveLetter, onOutputLine, cancellationToken);
        return Map(result);
    }

    public async Task<OptimizationRunOutcome> RunDefragmentAsync(
        string driveLetter, IProgress<string>? onOutputLine = null, CancellationToken cancellationToken = default)
    {
        var result = await DefragRunner.RunDefragmentAsync(driveLetter, onOutputLine, cancellationToken);
        return Map(result);
    }

    private static OptimizationRunOutcome Map(DefragRunResult result) =>
        new(result.Started, result.FailureReason, result.ExitCode, result.OutputLines);
}

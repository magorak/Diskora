using Diskora.Core.Models;
using Diskora.Core.Services;

namespace Diskora.Core.Tests;

public class DiskOptimizationServiceTests
{
    /// <summary>
    /// Fake enumerace disků - test se týká jen odvození typu z WMI dat, ne
    /// skutečného IOCTL, které na testovacím stroji vrací co vrátí.
    /// </summary>
    private sealed class FakeEnumeration(DiskMediaType? mediaType, int? physicalDiskIndex = 0) : IDiskEnumerationService
    {
        public IReadOnlyList<PhysicalDiskInfo> GetPhysicalDisks() => mediaType is null
            ? []
            : [new PhysicalDiskInfo(0, "Fake", 1000, mediaType.Value, DiskBusType.Usb, null)];

        public IReadOnlyList<VolumeInfo> GetVolumes() =>
            [new VolumeInfo("H:\\", "Test", "NTFS", 1000, 500, DriveType.Fixed, physicalDiskIndex)];
    }

    [Theory]
    [InlineData(DiskMediaType.HardDisk, false)]
    [InlineData(DiskMediaType.SolidState, true)]
    [InlineData(DiskMediaType.StorageClassMemory, true)]
    public void GetCapabilities_KdyzIoctlMlci_OdvodiTypZTypuMedia(DiskMediaType mediaType, bool expectedSolidState)
    {
        // Na tomhle stroji vrací IOCTL pro neexistující písmeno null, takže se
        // uplatní právě záložní cesta přes WMI - to je přesně to, co se testuje.
        var capabilities = new DiskOptimizationService(new FakeEnumeration(mediaType)).GetCapabilities("H:");

        Assert.Equal(expectedSolidState, capabilities.IsLikelySolidState);
    }

    [Fact]
    public void GetCapabilities_NeurcenyTypMedia_ZustavaNeznamy()
    {
        // Přesně případ USB disku: WMI hlásí neurčený typ. Radši přiznat nevědomost
        // než hádat - hádání by vedlo k doporučení defragmentace možného SSD.
        var capabilities = new DiskOptimizationService(new FakeEnumeration(DiskMediaType.Unknown)).GetCapabilities("H:");

        Assert.Null(capabilities.IsLikelySolidState);
    }

    [Fact]
    public void GetCapabilities_SvazekBezVazbyNaFyzickyDisk_ZustavaNeznamy()
    {
        var capabilities = new DiskOptimizationService(new FakeEnumeration(DiskMediaType.HardDisk, physicalDiskIndex: null))
            .GetCapabilities("H:");

        Assert.Null(capabilities.IsLikelySolidState);
    }

    [Fact]
    public void GetCapabilities_PrijimaPismenoVRuznychTvarech()
    {
        var service = new DiskOptimizationService(new FakeEnumeration(DiskMediaType.HardDisk));

        Assert.Equal(false, service.GetCapabilities("H:").IsLikelySolidState);
        Assert.Equal(false, service.GetCapabilities("H").IsLikelySolidState);
        Assert.Equal(false, service.GetCapabilities("H:\\").IsLikelySolidState);
    }
}

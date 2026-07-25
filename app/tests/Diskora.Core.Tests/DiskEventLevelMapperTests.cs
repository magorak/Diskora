using Diskora.Core.Models;

namespace Diskora.Core.Tests;

public class DiskEventLevelMapperTests
{
    [Theory]
    [InlineData((byte)1, DiskEventLevel.Critical)]
    [InlineData((byte)2, DiskEventLevel.Error)]
    [InlineData((byte)3, DiskEventLevel.Warning)]
    [InlineData((byte)4, DiskEventLevel.Information)]
    [InlineData((byte)0, DiskEventLevel.Unknown)]
    [InlineData((byte)5, DiskEventLevel.Unknown)]
    [InlineData((byte)200, DiskEventLevel.Unknown)]
    public void FromRawLevel_MapsKnownWinmetaLevels(byte? rawLevel, DiskEventLevel expected)
    {
        var result = DiskEventLevelMapper.FromRawLevel(rawLevel);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void FromRawLevel_NullLevel_MapsToUnknown()
    {
        var result = DiskEventLevelMapper.FromRawLevel(null);

        Assert.Equal(DiskEventLevel.Unknown, result);
    }
}

using Diskora.Core.Models;
using Diskora.Core.Services;

namespace Diskora.Core.Tests;

public sealed class DiskHealthChangeDetectorTests
{
    [Theory]
    [InlineData(DiskHealthStatus.Healthy, DiskHealthStatus.Warning, true)]
    [InlineData(DiskHealthStatus.Healthy, DiskHealthStatus.Critical, true)]
    [InlineData(DiskHealthStatus.Warning, DiskHealthStatus.Critical, true)]
    [InlineData(DiskHealthStatus.Healthy, DiskHealthStatus.Healthy, false)]
    [InlineData(DiskHealthStatus.Critical, DiskHealthStatus.Warning, false)]
    [InlineData(DiskHealthStatus.Warning, DiskHealthStatus.Healthy, false)]
    public void HasDegraded_ComparesSeverityCorrectly(DiskHealthStatus previous, DiskHealthStatus current, bool expected)
    {
        Assert.Equal(expected, DiskHealthChangeDetector.HasDegraded(previous, current));
    }

    [Fact]
    public void HasDegraded_NoPreviousReading_ReturnsFalse()
    {
        Assert.False(DiskHealthChangeDetector.HasDegraded(null, DiskHealthStatus.Critical));
    }

    [Theory]
    [InlineData(DiskHealthStatus.Unknown, DiskHealthStatus.Critical)]
    [InlineData(DiskHealthStatus.Healthy, DiskHealthStatus.Unknown)]
    public void HasDegraded_UnknownInvolved_ReturnsFalse(DiskHealthStatus previous, DiskHealthStatus current)
    {
        Assert.False(DiskHealthChangeDetector.HasDegraded(previous, current));
    }
}

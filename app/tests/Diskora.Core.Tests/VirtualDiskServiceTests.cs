using Diskora.Core.Models;
using Diskora.Core.Services;

namespace Diskora.Core.Tests;

public sealed class VirtualDiskServiceTests
{
    private sealed class FakeAttachmentRegistry : IVirtualDiskAttachmentRegistry
    {
        public int AttachedCallCount { get; private set; }

        public int DetachedCallCount { get; private set; }

        public void RecordAttached(string path, VirtualDiskFormat format, bool readOnly) => AttachedCallCount++;

        public void RecordDetached(string path) => DetachedCallCount++;

        public IReadOnlyList<AttachedVirtualDiskEntry> GetTrackedAttachments() => [];
    }

    [Fact]
    public void Attach_NonexistentPath_FailsAndDoesNotTouchRegistry()
    {
        var registry = new FakeAttachmentRegistry();
        var service = new VirtualDiskService(registry);
        var bogusPath = Path.Combine(Path.GetTempPath(), $"diskora-tests-missing-{Guid.NewGuid():N}.vhdx");

        var outcome = service.Attach(bogusPath, readOnly: true);

        Assert.False(outcome.Success);
        Assert.Equal(0, registry.AttachedCallCount);
    }

    [Fact]
    public void Detach_NonexistentPath_FailsAndDoesNotTouchRegistry()
    {
        var registry = new FakeAttachmentRegistry();
        var service = new VirtualDiskService(registry);
        var bogusPath = Path.Combine(Path.GetTempPath(), $"diskora-tests-missing-{Guid.NewGuid():N}.vhdx");

        var outcome = service.Detach(bogusPath);

        Assert.False(outcome.Success);
        Assert.Equal(0, registry.DetachedCallCount);
    }
}

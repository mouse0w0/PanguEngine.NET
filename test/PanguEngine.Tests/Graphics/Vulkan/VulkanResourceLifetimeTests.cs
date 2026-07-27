using PanguEngine.Graphics;
using PanguEngine.Graphics.Vulkan;

namespace PanguEngine.Tests.Graphics.Vulkan;

public sealed class VulkanResourceLifetimeTests
{
    [Fact]
    public void RequestDestroyWithoutHoldsEnqueuesAtInitialTimelineValue()
    {
        var resource = new TestResource();
        var destroyed = false;
        var enqueued = new List<(ulong RetireValue, Action Destroy)>();
        var lifetime = new VulkanResourceLifetime(
            resource,
            () => destroyed = true,
            (retireValue, destroy) => enqueued.Add((retireValue, destroy)));

        lifetime.RequestDestroy();

        var deletion = Assert.Single(enqueued);
        Assert.Equal(0ul, deletion.RetireValue);
        Assert.False(destroyed);
        deletion.Destroy();
        Assert.True(destroyed);
    }

    [Fact]
    public void RequestDestroyWaitsForLastHoldAndUsesLatestSubmissionValue()
    {
        var enqueued = new List<(ulong RetireValue, Action Destroy)>();
        var lifetime = CreateLifetime(enqueued);
        Assert.True(lifetime.TryAcquireHold());
        Assert.True(lifetime.TryAcquireHold());

        lifetime.ReleaseHold(9);
        lifetime.RequestDestroy();

        Assert.Empty(enqueued);
        lifetime.ReleaseHold(4);
        Assert.Equal(9ul, Assert.Single(enqueued).RetireValue);
    }

    [Fact]
    public void RequestDestroyRejectsNewHoldsAndOnlyEnqueuesOnce()
    {
        var enqueued = new List<(ulong RetireValue, Action Destroy)>();
        var lifetime = CreateLifetime(enqueued);
        Assert.True(lifetime.TryAcquireHold());

        lifetime.RequestDestroy();
        lifetime.RequestDestroy();

        Assert.False(lifetime.TryAcquireHold());
        lifetime.ReleaseHold();
        lifetime.RequestDestroy();
        Assert.Single(enqueued);
    }

    [Fact]
    public void RequestDestroyAfterReleasedHoldUsesRecordedSubmissionValue()
    {
        var enqueued = new List<(ulong RetireValue, Action Destroy)>();
        var lifetime = CreateLifetime(enqueued);
        Assert.True(lifetime.TryAcquireHold());

        lifetime.ReleaseHold(17);
        Assert.Empty(enqueued);
        lifetime.RequestDestroy();

        Assert.Equal(17ul, Assert.Single(enqueued).RetireValue);
    }

    [Fact]
    public void ReleaseHoldWithoutMatchingAcquireThrows()
    {
        var lifetime = CreateLifetime([]);

        Assert.Throws<InvalidOperationException>(() => lifetime.ReleaseHold());
    }

    [Fact]
    public void ResourceReturnsTheTrackedGraphicsResource()
    {
        var resource = new TestResource();
        var lifetime = new VulkanResourceLifetime(resource, () => { }, (_, _) => { });

        Assert.Same(resource, lifetime.Resource);
    }

    private static VulkanResourceLifetime CreateLifetime(List<(ulong RetireValue, Action Destroy)> enqueued)
    {
        return new VulkanResourceLifetime(
            new TestResource(),
            () => { },
            (retireValue, destroy) => enqueued.Add((retireValue, destroy)));
    }

    private sealed class TestResource : GraphicsResource
    {
        public override void Destroy()
        {
            MarkDestroyed();
        }
    }
}
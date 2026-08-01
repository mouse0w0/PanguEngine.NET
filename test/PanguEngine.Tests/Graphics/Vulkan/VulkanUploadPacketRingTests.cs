using PanguEngine.Graphics.Vulkan;
using Silk.NET.Vulkan;

namespace PanguEngine.Tests.Graphics.Vulkan;

public sealed class VulkanUploadPacketRingTests
{
    [Fact]
    public void RingStartsWithThreeAvailablePackets()
    {
        var ring = CreateRing();

        Assert.Equal(3, VulkanUploadPacketRing.Capacity);
        Assert.Equal(0, ring.SubmittedCount);
        Assert.False(ring.NextPacket.IsSubmitted);
    }

    [Fact]
    public void CommitNextSubmissionAdvancesToNextPacket()
    {
        var pool = CreatePool();
        var ring = CreateRing();
        var lease = pool.BeginBatch();
        var handle = new VulkanUploadHandle();

        ring.EnsureNextAvailable();
        ring.CommitNextSubmission(4, lease, [handle]);

        Assert.Equal(1, ring.SubmittedCount);
        Assert.False(ring.NextPacket.IsSubmitted);
        Assert.Equal(4ul, ring.OldestSubmittedPacket.SubmissionValue);
        pool.Destroy();
    }

    [Fact]
    public void RetireCompletesPacketsInSubmissionOrder()
    {
        var pool = CreatePool();
        var ring = CreateRing();
        var first = new VulkanUploadHandle();
        var second = new VulkanUploadHandle();
        ring.CommitNextSubmission(4, pool.BeginBatch(), [first]);
        ring.CommitNextSubmission(7, pool.BeginBatch(), [second]);

        ring.RetireCompleted(4, pool);

        Assert.True(first.IsSucceeded);
        Assert.False(second.IsCompleted);
        Assert.Equal(1, ring.SubmittedCount);
        Assert.Equal(7ul, ring.OldestSubmittedPacket.SubmissionValue);
        ring.RetireCompleted(7, pool);
        pool.Destroy();
    }

    [Fact]
    public void RetireDoesNotOverwriteFaultedHandle()
    {
        var pool = CreatePool();
        var ring = CreateRing();
        var expected = new InvalidOperationException("Upload fault.");
        var handle = new VulkanUploadHandle();
        ring.CommitNextSubmission(3, pool.BeginBatch(), [handle]);
        handle.SignalFailure(expected);

        ring.RetireCompleted(3, pool);

        Assert.Same(expected, handle.Exception);
        Assert.True(handle.IsFaulted);
        pool.Destroy();
    }

    [Fact]
    public void FullRingPointsNextAtOldestSubmittedPacket()
    {
        var pool = CreatePool();
        var ring = CreateRing();
        ring.CommitNextSubmission(2, pool.BeginBatch(), []);
        ring.CommitNextSubmission(4, pool.BeginBatch(), []);
        ring.CommitNextSubmission(6, pool.BeginBatch(), []);

        Assert.Equal(3, ring.SubmittedCount);
        Assert.Same(ring.OldestSubmittedPacket, ring.NextPacket);
        Assert.Throws<InvalidOperationException>(ring.EnsureNextAvailable);
        ring.RetireCompleted(6, pool);
        pool.Destroy();
    }

    [Fact]
    public void FaultSubmittedHandlesKeepsPacketsSubmitted()
    {
        var pool = CreatePool();
        var ring = CreateRing();
        var expected = new InvalidOperationException("Timeline query failed.");
        var handle = new VulkanUploadHandle();
        ring.CommitNextSubmission(5, pool.BeginBatch(), [handle]);

        ring.FaultSubmittedHandles(expected);

        Assert.True(handle.IsFaulted);
        Assert.Equal(1, ring.SubmittedCount);
        Assert.True(ring.OldestSubmittedPacket.IsSubmitted);
        ring.ReclaimAll(pool, completeSuccessfully: false, exception: expected);
        pool.Destroy();
    }

    private static VulkanUploadPacketRing CreateRing()
    {
        return new VulkanUploadPacketRing(new CommandBuffer[VulkanUploadPacketRing.Capacity]);
    }

    private static VulkanStagingPagePool CreatePool()
    {
        return new VulkanStagingPagePool(
            4,
            (capacity, dedicated, identity) => new VulkanStagingPage(
                null!, 0, capacity, dedicated, identity, (_, _) => { }, () => { }));
    }
}

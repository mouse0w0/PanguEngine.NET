using PanguEngine.Graphics.Vulkan;

namespace PanguEngine.Tests.Graphics.Vulkan;

public sealed class UploadHandleTests
{
    [Fact]
    public void PendingHandleIsNotReadyAndThrowIfNotReadyReportsStateError()
    {
        var handle = new VulkanUploadHandle();

        Assert.False(handle.IsReady);
        Assert.False(handle.IsCompleted);
        Assert.False(handle.IsSucceeded);
        Assert.False(handle.IsFaulted);
        Assert.Null(handle.Exception);

        Assert.Throws<InvalidOperationException>(handle.ThrowIfNotReady);
    }

    [Fact]
    public void ReadyHandleCanBeConsumedBeforeCompletion()
    {
        var handle = new VulkanUploadHandle();
        handle.SignalReady();

        Assert.True(handle.IsReady);
        Assert.False(handle.IsCompleted);
        Assert.False(handle.IsSucceeded);
        Assert.False(handle.IsFaulted);
        Assert.Null(handle.Exception);

        handle.ThrowIfNotReady();
    }

    [Fact]
    public void SucceededHandleRemainsReady()
    {
        var handle = new VulkanUploadHandle();
        handle.SignalSuccess();

        Assert.True(handle.IsReady);
        Assert.True(handle.IsCompleted);
        Assert.True(handle.IsSucceeded);
        Assert.False(handle.IsFaulted);
        Assert.Null(handle.Exception);

        handle.ThrowIfNotReady();
    }

    [Fact]
    public void ReadyToFaultedRevokesReadinessAndRethrowsSameException()
    {
        var expected = new InvalidOperationException("Upload failed.");
        var handle = new VulkanUploadHandle();
        handle.SignalReady();
        handle.SignalFailure(expected);

        Assert.False(handle.IsReady);
        Assert.True(handle.IsCompleted);
        Assert.False(handle.IsSucceeded);
        Assert.True(handle.IsFaulted);
        Assert.Same(expected, handle.Exception);

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(handle.ThrowIfNotReady));
    }
}

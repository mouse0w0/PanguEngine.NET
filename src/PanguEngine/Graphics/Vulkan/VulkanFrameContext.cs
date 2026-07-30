using Silk.NET.Vulkan;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Represents Vulkan resources assigned to one in-flight frame slot.
/// </summary>
internal sealed unsafe class VulkanFrameContext
{
    private readonly VulkanCommandPool _commandPool;
    private readonly Semaphore _imageAvailableSemaphore;
    private readonly Fence _fence;
    private bool _destroyed;

    /// <summary>
    /// Creates resources for an in-flight frame slot.
    /// </summary>
    internal VulkanFrameContext()
    {
        _commandPool = new VulkanCommandPool();

        try
        {
            CommandBuffer = _commandPool.AllocateCommandBuffer();
            CommandList = new VulkanCommandList(CommandBuffer);

            SemaphoreCreateInfo semaphoreInfo = new()
            {
                SType = StructureType.SemaphoreCreateInfo
            };
            if (VulkanContext.Vk.CreateSemaphore(
                    VulkanContext.Device, in semaphoreInfo, null, out var imageAvailableSemaphore) != Result.Success)
                throw new InvalidOperationException("Failed to create image-available semaphore.");
            _imageAvailableSemaphore = imageAvailableSemaphore;

            FenceCreateInfo fenceInfo = new()
            {
                SType = StructureType.FenceCreateInfo
            };
            if (VulkanContext.Vk.CreateFence(VulkanContext.Device, in fenceInfo, null, out var fence) != Result.Success)
                throw new InvalidOperationException("Failed to create in-flight fence.");
            _fence = fence;
        }
        catch
        {
            if (_fence.Handle != 0)
                VulkanContext.Vk.DestroyFence(VulkanContext.Device, _fence, null);
            if (_imageAvailableSemaphore.Handle != 0)
                VulkanContext.Vk.DestroySemaphore(VulkanContext.Device, _imageAvailableSemaphore, null);
            _commandPool.Destroy();
            throw;
        }
    }

    /// <summary>
    /// Gets the primary command buffer assigned to this frame slot.
    /// </summary>
    internal CommandBuffer CommandBuffer { get; }

    /// <summary>
    /// Gets the command list assigned to this frame slot.
    /// </summary>
    internal VulkanCommandList CommandList { get; }

    /// <summary>
    /// Gets the semaphore signaled when a swapchain image becomes available.
    /// </summary>
    internal Semaphore ImageAvailableSemaphore => _imageAvailableSemaphore;

    /// <summary>
    /// Gets the fence signaled when this frame slot's submission completes.
    /// </summary>
    internal Fence Fence => _fence;

    /// <summary>
    /// Gets whether this frame slot has a pending GPU submission.
    /// </summary>
    internal bool HasPendingSubmission { get; private set; }

    /// <summary>
    /// Waits until this frame slot can be safely reused.
    /// </summary>
    internal void WaitForReuse()
    {
        if (!HasPendingSubmission)
            return;

        if (VulkanContext.Vk.WaitForFences(
                VulkanContext.Device, 1, in _fence, true, ulong.MaxValue) != Result.Success)
            throw new InvalidOperationException("Failed to wait for in-flight fence.");

        HasPendingSubmission = false;
    }

    /// <summary>
    /// Prepares the command resources for a new frame.
    /// </summary>
    internal void ResetCommands()
    {
        if (HasPendingSubmission)
            throw new InvalidOperationException("Cannot reset commands while the frame is pending.");

        _commandPool.Reset();
        CommandList.Reset();
    }

    /// <summary>
    /// Prepares the completion fence for a new submission.
    /// </summary>
    internal void ResetFenceForSubmit()
    {
        if (HasPendingSubmission)
            throw new InvalidOperationException("Cannot reset the fence while the frame is pending.");

        if (VulkanContext.Vk.ResetFences(VulkanContext.Device, 1, in _fence) != Result.Success)
            throw new InvalidOperationException("Failed to reset in-flight fence.");
    }

    /// <summary>
    /// Marks this frame slot as having a pending GPU submission.
    /// </summary>
    internal void MarkSubmitted()
    {
        HasPendingSubmission = true;
    }

    /// <summary>
    /// Destroys the Vulkan resources owned by this frame slot.
    /// </summary>
    internal void Destroy()
    {
        if (_destroyed)
            return;

        CommandList.Invalidate();
        if (_fence.Handle != 0)
            VulkanContext.Vk.DestroyFence(VulkanContext.Device, _fence, null);
        if (_imageAvailableSemaphore.Handle != 0)
            VulkanContext.Vk.DestroySemaphore(VulkanContext.Device, _imageAvailableSemaphore, null);
        _commandPool.Destroy();
        _destroyed = true;
    }
}

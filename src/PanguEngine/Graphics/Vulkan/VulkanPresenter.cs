using Silk.NET.Vulkan;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Vulkan implementation of <see cref="Presenter"/>.
/// </summary>
internal sealed unsafe class VulkanPresenter : Presenter
{
    private readonly VulkanWindow _window;
    private readonly VulkanCommandPool _commandPool;
    private readonly VulkanCommandList _commandList = new();
    private bool _destroyed;
    private VulkanFrame? _currentFrame;

    /// <summary>
    /// Creates a Vulkan graphics presenter for a window.
    /// </summary>
    /// <param name="window">The Vulkan window used for presentation.</param>
    internal VulkanPresenter(VulkanWindow window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _commandPool = new VulkanCommandPool();
    }

    /// <inheritdoc/>
    public override bool IsDestroyed => _destroyed;

    /// <inheritdoc/>
    public override uint Width => _window.Extent.Width;

    /// <inheritdoc/>
    public override uint Height => _window.Extent.Height;

    /// <inheritdoc/>
    public override TextureFormat ColorFormat => VulkanGraphicsDevice.FromVulkanFormat(_window.ImageFormat);

    /// <inheritdoc/>
    public override Frame BeginFrame()
    {
        ThrowIfDestroyed();
        if (_currentFrame != null)
            throw new InvalidOperationException("A graphics frame is already active.");

        _window.WaitForInFlightFence();
        VulkanUploader.FlushPendingUploads();

        var result = _window.AcquireNextImage(out var imageIndex);
        if (result == Result.ErrorOutOfDateKhr)
            result = _window.AcquireNextImage(out imageIndex);

        if (result == Result.ErrorOutOfDateKhr)
            throw new InvalidOperationException("Failed to acquire a renderable swapchain image after recreation.");

        var commandBuffer = _commandPool.CommandBuffers[_window.CurrentFrame];
        VulkanContext.Vk.ResetCommandBuffer(commandBuffer, 0);

        var frame = new VulkanFrame(
            imageIndex,
            _window.Images[imageIndex],
            _window.ImageViews[imageIndex],
            _window.Extent.Width,
            _window.Extent.Height,
            ColorFormat,
            _commandList);

        _commandList.Reset(frame, commandBuffer);
        _currentFrame = frame;
        return frame;
    }

    /// <inheritdoc/>
    public override void EndFrame(Frame frame)
    {
        ThrowIfDestroyed();

        var vulkanFrame = frame as VulkanFrame
                          ?? throw new InvalidOperationException(
                              "Graphics frame was not created by the Vulkan backend.");

        if (!ReferenceEquals(vulkanFrame, _currentFrame) || !vulkanFrame.IsValid)
            throw new InvalidOperationException("Graphics frame is not the active frame for this presenter.");

        var commandBuffer = _commandPool.CommandBuffers[_window.CurrentFrame];
        var timelineValue = VulkanContext.NextGlobalTimelineValue();

        try
        {
            vulkanFrame.VulkanCommandList.CompleteForSubmit();
            Submit(commandBuffer, timelineValue);
            _window.PresentImage(vulkanFrame.ImageIndex);
            _window.AdvanceFrame();
            VulkanDeletionQueue.Collect();
        }
        finally
        {
            vulkanFrame.Invalidate();
            _currentFrame = null;
        }
    }

    /// <inheritdoc/>
    public override void Destroy()
    {
        if (_destroyed)
            return;

        VulkanContext.Vk.DeviceWaitIdle(VulkanContext.Device);
        _currentFrame?.Invalidate();
        _currentFrame = null;
        _commandPool.Destroy();
        _destroyed = true;
    }

    private void ThrowIfDestroyed()
    {
        if (_destroyed)
            throw new ObjectDisposedException(nameof(VulkanPresenter));
    }

    private void Submit(CommandBuffer commandBuffer, ulong timelineValue)
    {
        _window.ResetInFlightFence();

        var waitSemaphores = stackalloc[] { _window.GetImageAvailableSemaphore() };
        var waitStages = stackalloc[] { PipelineStageFlags.ColorAttachmentOutputBit };
        var signalSemaphores = stackalloc[]
            { _window.GetRenderFinishedSemaphore(), VulkanContext.GlobalTimelineSemaphore };

        var signalValues = stackalloc[] { 0UL, timelineValue };

        TimelineSemaphoreSubmitInfo timelineSubmitInfo = new()
        {
            SType = StructureType.TimelineSemaphoreSubmitInfo,
            SignalSemaphoreValueCount = 2,
            PSignalSemaphoreValues = signalValues,
        };

        SubmitInfo submitInfo = new()
        {
            SType = StructureType.SubmitInfo,
            PNext = &timelineSubmitInfo,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = waitSemaphores,
            PWaitDstStageMask = waitStages,
            CommandBufferCount = 1,
            PCommandBuffers = &commandBuffer,
            SignalSemaphoreCount = 2,
            PSignalSemaphores = signalSemaphores,
        };

        if (VulkanContext.Vk.QueueSubmit(VulkanContext.GraphicsQueue, 1, in submitInfo, _window.GetInFlightFence()) !=
            Result.Success)
            throw new InvalidOperationException("Failed to submit draw command buffer.");
    }
}
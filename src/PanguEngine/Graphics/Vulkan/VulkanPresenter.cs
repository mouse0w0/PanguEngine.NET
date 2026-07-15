using System.Diagnostics.CodeAnalysis;
using Silk.NET.Vulkan;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Vulkan implementation of <see cref="Presenter"/>.
/// </summary>
internal sealed unsafe class VulkanPresenter : Presenter
{
    private readonly VulkanWindow _window;
    private readonly VulkanFrameContext[] _frameContexts;
    private uint _frameSlot;
    private ulong _nextFrameNumber;
    private bool _destroyed;
    private bool _faulted;
    private VulkanFrame? _currentFrame;

    /// <summary>
    /// Creates a Vulkan graphics presenter for a window.
    /// </summary>
    /// <param name="window">The Vulkan window used for presentation.</param>
    internal VulkanPresenter(VulkanWindow window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _frameContexts = new VulkanFrameContext[VulkanContext.MaxFramesInFlight];

        var createdCount = 0;
        try
        {
            for (; createdCount < _frameContexts.Length; createdCount++)
                _frameContexts[createdCount] = new VulkanFrameContext();
        }
        catch
        {
            for (var i = 0; i < createdCount; i++)
                _frameContexts[i].Destroy();
            throw;
        }
    }

    /// <inheritdoc/>
    // ReSharper disable once ConvertToAutoPropertyWithPrivateSetter
    public override bool IsDestroyed => _destroyed;

    /// <inheritdoc/>
    public override uint Width => _window.Extent.Width;

    /// <inheritdoc/>
    public override uint Height => _window.Extent.Height;

    /// <inheritdoc/>
    public override TextureFormat ColorFormat => VulkanMapping.FromVulkanFormat(_window.ImageFormat);

    /// <inheritdoc/>
    public override uint MaxFramesInFlight => VulkanContext.MaxFramesInFlight;

    /// <inheritdoc/>
    public override Frame BeginFrame()
    {
        if (TryBeginFrame(out var frame))
            return frame;

        throw new InvalidOperationException("Failed to acquire a renderable swapchain image after recreation.");
    }

    /// <inheritdoc/>
    public override bool TryBeginFrame([MaybeNullWhen(false)] out Frame frame)
    {
        EnsureUsable();
        if (_currentFrame != null)
            throw new InvalidOperationException("A graphics frame is already active.");

        frame = null;
        var context = _frameContexts[_frameSlot];

        try
        {
            context.WaitForReuse();
            VulkanUploader.FlushPendingUploads();

            var result = _window.AcquireNextImage(context.ImageAvailableSemaphore, out var imageIndex);
            if (result == Result.ErrorOutOfDateKhr)
                result = _window.AcquireNextImage(context.ImageAvailableSemaphore, out imageIndex);

            if (result == Result.ErrorOutOfDateKhr)
                return false;

            context.ResetCommands();

            var vulkanFrame = new VulkanFrame(
                _nextFrameNumber++,
                _frameSlot,
                imageIndex,
                _window.Extent.Width,
                _window.Extent.Height,
                _window.ColorOutputs[imageIndex]!,
                context);

            _currentFrame = vulkanFrame;
            frame = vulkanFrame;
            return true;
        }
        catch
        {
            _faulted = true;
            throw;
        }
    }

    /// <inheritdoc/>
    public override void EndFrame(Frame frame)
    {
        EnsureUsable();

        var vulkanFrame = frame as VulkanFrame
                          ?? throw new InvalidOperationException(
                              "Graphics frame was not created by the Vulkan backend.");

        if (!ReferenceEquals(vulkanFrame, _currentFrame) || !vulkanFrame.IsValid)
            throw new InvalidOperationException("Graphics frame is not the active frame for this presenter.");

        var context = vulkanFrame.FrameContext;

        try
        {
            vulkanFrame.VulkanCommandList.CompleteForSubmit();

            var renderFinishedSemaphore = _window.GetRenderFinishedSemaphore(vulkanFrame.ImageIndex);
            var timelineValue = VulkanContext.NextGlobalTimelineValue();

            context.ResetFenceForSubmit();
            Submit(context, renderFinishedSemaphore, timelineValue);
            context.MarkSubmitted();
            _window.PresentImage(vulkanFrame.ImageIndex, renderFinishedSemaphore);

            if (!vulkanFrame.VulkanColorOutput.IsDestroyed)
                vulkanFrame.VulkanColorOutput.VulkanTexture.ResetLayout();

            _frameSlot = (_frameSlot + 1) % VulkanContext.MaxFramesInFlight;
            VulkanDeletionQueue.Collect();
        }
        catch
        {
            _faulted = true;
            throw;
        }
        finally
        {
            vulkanFrame.Invalidate();
            _currentFrame = null;
        }
    }

    /// <inheritdoc/>
    internal override void Destroy()
    {
        if (_destroyed)
            return;

        VulkanContext.Vk.DeviceWaitIdle(VulkanContext.Device);
        _currentFrame?.Invalidate();
        _currentFrame = null;

        foreach (var context in _frameContexts)
            context.Destroy();

        _destroyed = true;
    }

    private static void Submit(
        VulkanFrameContext context,
        Semaphore renderFinishedSemaphore,
        ulong timelineValue)
    {
        var commandBuffer = context.CommandBuffer;
        var waitSemaphores = stackalloc[] { context.ImageAvailableSemaphore };
        var waitStages = stackalloc[] { PipelineStageFlags.ColorAttachmentOutputBit };
        var signalSemaphores = stackalloc[]
            { renderFinishedSemaphore, VulkanContext.GlobalTimelineSemaphore };

        var signalValues = stackalloc[] { 0UL, timelineValue };

        TimelineSemaphoreSubmitInfo timelineSubmitInfo = new()
        {
            SType = StructureType.TimelineSemaphoreSubmitInfo,
            SignalSemaphoreValueCount = 2,
            PSignalSemaphoreValues = signalValues
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
            PSignalSemaphores = signalSemaphores
        };

        if (VulkanContext.Vk.QueueSubmit(VulkanContext.GraphicsQueue, 1, in submitInfo, context.Fence) !=
            Result.Success)
            throw new InvalidOperationException("Failed to submit draw command buffer.");
    }

    /// <summary>
    /// Ensures this presenter can begin or end a frame.
    /// </summary>
    private void EnsureUsable()
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
        if (_faulted)
            throw new InvalidOperationException("The graphics presenter is faulted and can no longer submit frames.");
    }
}
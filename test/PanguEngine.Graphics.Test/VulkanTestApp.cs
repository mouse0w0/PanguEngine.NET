using PanguEngine.Graphics.Vulkan;
using Silk.NET.Core.Native;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;

namespace PanguEngine.Graphics.Test;

public sealed unsafe class VulkanTestApp(IVulkanTestScene scene)
{
    private VulkanWindow? _vulkanWindow;
    private VulkanCommandPool? _commandPool;
    private bool _sceneInitialized;
    private bool _windowInitialized;
    private bool _uploaderInitialized;
    private bool _allocatorInitialized;
    private bool _contextInitialized;
    private bool _engineInitialized;

    public void Run()
    {
        try
        {
            Initialize();

            _vulkanWindow!.Window.Render += _ => DrawFrame();

            _vulkanWindow.Window.Run();
        }
        finally
        {
            Shutdown();
        }
    }

    private void Initialize()
    {
        Engine.Initialize();
        _engineInitialized = true;

        var options = WindowOptions.DefaultVulkan with
        {
            Size = new Vector2D<int>(800, 600),
            Title = scene.Name
        };

        var window = Window.Create(options);
        window.Initialize();

        if (window.VkSurface is null)
            throw new InvalidOperationException("Windowing platform doesn't support Vulkan.");

        var glfwExtensions = window.VkSurface.GetRequiredExtensions(out var count);
        var requiredExtensions = SilkMarshal.PtrToStringArray((nint)glfwExtensions, (int)count);

        VulkanContext.InitializeInstance(requiredExtensions);
        _contextInitialized = true;

        var surface = window.VkSurface.Create<AllocationCallbacks>(VulkanContext.VkInstance.ToHandle(), null)
            .ToSurface();
        VulkanContext.InitializeDevice(surface);

        VulkanAllocator.Initialize();
        _allocatorInitialized = true;

        VulkanUploader.Initialize();
        _uploaderInitialized = true;

        _vulkanWindow = new VulkanWindow(window, surface);
        _windowInitialized = true;

        _commandPool = new VulkanCommandPool();
        scene.Initialize(_vulkanWindow);
        _sceneInitialized = true;
    }

    private void DrawFrame()
    {
        var window = _vulkanWindow!;

        scene.PrepareFrame();

        window.WaitForInFlightFence();

        VulkanUploader.FlushPendingUploads();

        var timelineValue = VulkanContext.NextGlobalTimelineValue();
        var result = window.AcquireNextImage(out var imageIndex);
        if (result == Result.ErrorOutOfDateKhr)
            return;

        window.ResetInFlightFence();

        var commandBuffer = _commandPool!.CommandBuffers[window.CurrentFrame];
        VulkanContext.Vk.ResetCommandBuffer(commandBuffer, 0);

        CommandBufferBeginInfo beginInfo = new()
        {
            SType = StructureType.CommandBufferBeginInfo,
        };

        if (VulkanContext.Vk.BeginCommandBuffer(commandBuffer, &beginInfo) != Result.Success)
            throw new InvalidOperationException("Failed to begin recording command buffer.");

        TransitionToColorAttachment(commandBuffer, window.Images[imageIndex]);
        scene.Record(commandBuffer, window.ImageViews[imageIndex], window.Extent, window.ImageFormat);
        TransitionToPresent(commandBuffer, window.Images[imageIndex]);

        if (VulkanContext.Vk.EndCommandBuffer(commandBuffer) != Result.Success)
            throw new InvalidOperationException("Failed to record command buffer.");

        Submit(commandBuffer, timelineValue);

        window.PresentImage(imageIndex);
        window.AdvanceFrame();

        VulkanDeletionQueue.Collect();
    }

    private static void TransitionToColorAttachment(CommandBuffer commandBuffer, Image image)
    {
        ImageMemoryBarrier2 barrier = new()
        {
            SType = StructureType.ImageMemoryBarrier2,
            SrcStageMask = PipelineStageFlags2.TopOfPipeBit,
            SrcAccessMask = AccessFlags2.None,
            DstStageMask = PipelineStageFlags2.ColorAttachmentOutputBit,
            DstAccessMask = AccessFlags2.ColorAttachmentWriteBit,
            OldLayout = ImageLayout.Undefined,
            NewLayout = ImageLayout.ColorAttachmentOptimal,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = image,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },
        };

        DependencyInfo dependency = new()
        {
            SType = StructureType.DependencyInfo,
            ImageMemoryBarrierCount = 1,
            PImageMemoryBarriers = &barrier,
        };

        VulkanContext.Vk.CmdPipelineBarrier2(commandBuffer, &dependency);
    }

    private static void TransitionToPresent(CommandBuffer commandBuffer, Image image)
    {
        ImageMemoryBarrier2 barrier = new()
        {
            SType = StructureType.ImageMemoryBarrier2,
            SrcStageMask = PipelineStageFlags2.ColorAttachmentOutputBit,
            SrcAccessMask = AccessFlags2.ColorAttachmentWriteBit,
            DstStageMask = PipelineStageFlags2.BottomOfPipeBit,
            DstAccessMask = AccessFlags2.None,
            OldLayout = ImageLayout.ColorAttachmentOptimal,
            NewLayout = ImageLayout.PresentSrcKhr,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = image,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },
        };

        DependencyInfo dependency = new()
        {
            SType = StructureType.DependencyInfo,
            ImageMemoryBarrierCount = 1,
            PImageMemoryBarriers = &barrier,
        };

        VulkanContext.Vk.CmdPipelineBarrier2(commandBuffer, &dependency);
    }

    private void Submit(CommandBuffer commandBuffer, ulong timelineValue)
    {
        var window = _vulkanWindow!;

        var waitSemaphores = stackalloc[] { window.GetImageAvailableSemaphore() };
        var waitStages = stackalloc[] { PipelineStageFlags.ColorAttachmentOutputBit };
        var signalSemaphores = stackalloc[]
            { window.GetRenderFinishedSemaphore(), VulkanContext.GlobalTimelineSemaphore };
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

        if (VulkanContext.Vk.QueueSubmit(VulkanContext.GraphicsQueue, 1, in submitInfo, window.GetInFlightFence()) !=
            Result.Success)
            throw new InvalidOperationException("Failed to submit draw command buffer.");
    }

    private void Shutdown()
    {
        if (_contextInitialized)
            VulkanContext.Vk.DeviceWaitIdle(VulkanContext.Device);

        if (_sceneInitialized)
            scene.Destroy();

        _commandPool?.Destroy();

        if (_windowInitialized)
            _vulkanWindow!.Destroy();

        if (_uploaderInitialized)
            VulkanUploader.Destroy();

        if (_allocatorInitialized)
        {
            VulkanDeletionQueue.Drain();
            VulkanAllocator.Destroy();
        }

        if (_contextInitialized)
            VulkanContext.Destroy();

        if (_engineInitialized)
            Engine.Shutdown();
    }
}
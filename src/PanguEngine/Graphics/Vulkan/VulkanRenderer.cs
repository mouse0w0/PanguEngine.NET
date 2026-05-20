using Silk.NET.Vulkan;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Handles Vulkan graphics pipeline creation, command recording, and frame presentation.
/// </summary>
public sealed unsafe class VulkanRenderer
{
    private readonly VulkanWindow _window;
    private readonly VulkanCommandPool _commandPool;

    private readonly Shader _vertexShader;
    private readonly Shader _fragmentShader;
    private readonly GraphicsPipeline _pipeline;

    /// <summary>
    /// Initializes the renderer by loading shaders, creating the graphics pipeline, and allocating a command pool.
    /// </summary>
    /// <param name="window">The Vulkan swapchain window used for rendering.</param>
    public VulkanRenderer(VulkanWindow window)
    {
        _window = window;

        var basePath = AppContext.BaseDirectory;
        var vertPath = Path.Combine(basePath, "Assets", "Shaders", "triangle.vert");
        var fragPath = Path.Combine(basePath, "Assets", "Shaders", "triangle.frag");

        var vertSource = File.ReadAllText(vertPath);
        var fragSource = File.ReadAllText(fragPath);

        _vertexShader = GraphicsContext.Device.CreateShader(new ShaderDescription(
            ShaderStage.Vertex,
            vertSource,
            Name: "triangle.vert"));
        _fragmentShader = GraphicsContext.Device.CreateShader(new ShaderDescription(
            ShaderStage.Fragment,
            fragSource,
            Name: "triangle.frag"));
        _pipeline = GraphicsContext.Device.CreateGraphicsPipeline(new GraphicsPipelineDescription(
            new[] { _vertexShader, _fragmentShader },
            VertexInputDescription.Empty,
            ColorAttachmentFormat: VulkanGraphicsDevice.FromVulkanFormat(_window.ImageFormat)));

        _commandPool = new VulkanCommandPool();
    }

    /// <summary>
    /// Records and submits a draw command for the current frame, then presents the rendered image.
    /// </summary>
    /// <param name="delta">The time elapsed since the last frame, in seconds.</param>
    public void DrawFrame(double delta)
    {
        _window.WaitForInFlightFence();
        VulkanUploader.FlushPendingUploads();

        var timelineValue = VulkanContext.NextGlobalTimelineValue();

        var result = _window.AcquireNextImage(out var imageIndex);
        if (result == Result.ErrorOutOfDateKhr)
            return;

        _window.ResetInFlightFence();

        var commandBuffer = _commandPool.CommandBuffers[_window.CurrentFrame];
        VulkanContext.Vk.ResetCommandBuffer(commandBuffer, 0);

        CommandBufferBeginInfo beginInfo = new()
        {
            SType = StructureType.CommandBufferBeginInfo,
        };

        if (VulkanContext.Vk.BeginCommandBuffer(commandBuffer, &beginInfo) != Result.Success)
            throw new InvalidOperationException("Failed to begin recording command buffer.");

        var swapchainImage = _window.Images[imageIndex];
        var swapchainImageView = _window.ImageViews[imageIndex];
        var extent = _window.Extent;

        ImageMemoryBarrier2 preRenderBarrier = new()
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
            Image = swapchainImage,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },
        };

        DependencyInfo preRenderDep = new()
        {
            SType = StructureType.DependencyInfo,
            ImageMemoryBarrierCount = 1,
            PImageMemoryBarriers = &preRenderBarrier,
        };

        VulkanContext.Vk.CmdPipelineBarrier2(commandBuffer, &preRenderDep);

        ClearValue clearColor = new()
        {
            Color = new ClearColorValue { Float32_0 = 0, Float32_1 = 0, Float32_2 = 0, Float32_3 = 1 },
        };

        RenderingAttachmentInfo colorAttachment = new()
        {
            SType = StructureType.RenderingAttachmentInfo,
            ImageView = swapchainImageView,
            ImageLayout = ImageLayout.ColorAttachmentOptimal,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            ClearValue = clearColor,
        };

        RenderingInfo renderingInfo = new()
        {
            SType = StructureType.RenderingInfo,
            RenderArea = new Rect2D { Offset = new Offset2D(0, 0), Extent = extent },
            LayerCount = 1,
            ColorAttachmentCount = 1,
            PColorAttachments = &colorAttachment,
        };

        VulkanContext.Vk.CmdBeginRendering(commandBuffer, &renderingInfo);
        VulkanContext.Vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, GetVulkanPipeline().Pipeline);

        Viewport viewport = new()
        {
            X = 0,
            Y = 0,
            Width = extent.Width,
            Height = extent.Height,
            MinDepth = 0,
            MaxDepth = 1,
        };
        Rect2D scissor = new()
        {
            Offset = { X = 0, Y = 0 },
            Extent = extent,
        };
        VulkanContext.Vk.CmdSetViewport(commandBuffer, 0, 1, &viewport);
        VulkanContext.Vk.CmdSetScissor(commandBuffer, 0, 1, &scissor);

        VulkanContext.Vk.CmdDraw(commandBuffer, 3, 1, 0, 0);
        VulkanContext.Vk.CmdEndRendering(commandBuffer);

        ImageMemoryBarrier2 postRenderBarrier = new()
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
            Image = swapchainImage,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },
        };

        DependencyInfo postRenderDep = new()
        {
            SType = StructureType.DependencyInfo,
            ImageMemoryBarrierCount = 1,
            PImageMemoryBarriers = &postRenderBarrier,
        };

        VulkanContext.Vk.CmdPipelineBarrier2(commandBuffer, &postRenderDep);

        if (VulkanContext.Vk.EndCommandBuffer(commandBuffer) != Result.Success)
            throw new InvalidOperationException("Failed to record command buffer.");

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

        _window.PresentImage(imageIndex);
        _window.AdvanceFrame();

        VulkanDeletionQueue.Collect();
    }

    /// <summary>
    /// Destroys the graphics pipeline, command pool, and shader modules, releasing all GPU resources.
    /// </summary>
    public void Destroy()
    {
        VulkanContext.Vk.DeviceWaitIdle(VulkanContext.Device);

        _commandPool.Destroy();
        _pipeline.Destroy();
        _fragmentShader.Destroy();
        _vertexShader.Destroy();
    }

    private VulkanGraphicsPipeline GetVulkanPipeline()
    {
        return _pipeline as VulkanGraphicsPipeline
               ?? throw new InvalidOperationException(
                   "Renderer graphics pipeline was not created by the Vulkan backend.");
    }
}
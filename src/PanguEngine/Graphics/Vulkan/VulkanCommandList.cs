using Silk.NET.Vulkan;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Vulkan implementation of <see cref="CommandList"/>.
/// </summary>
internal sealed unsafe class VulkanCommandList : CommandList
{
    private VulkanFrame? _frame;
    private CommandBuffer _commandBuffer;
    private bool _begun;
    private bool _ended;
    private bool _rendering;
    private bool _presentTransitionRecorded;
    private bool _valid;
    private VulkanGraphicsPipeline? _graphicsPipeline;

    /// <summary>
    /// Gets whether command recording has ended.
    /// </summary>
    internal bool IsEnded => _ended;

    /// <summary>
    /// Gets whether command recording has begun.
    /// </summary>
    internal bool IsBegun => _begun;

    /// <summary>
    /// Resets this command list for a frame.
    /// </summary>
    /// <param name="frame">The owning frame.</param>
    /// <param name="commandBuffer">The command buffer to record.</param>
    internal void Reset(VulkanFrame frame, CommandBuffer commandBuffer)
    {
        _frame = frame;
        _commandBuffer = commandBuffer;
        _begun = false;
        _ended = false;
        _rendering = false;
        _presentTransitionRecorded = false;
        _valid = true;
        _graphicsPipeline = null;
    }

    /// <inheritdoc/>
    public override void Begin()
    {
        EnsureUsable();
        if (_begun)
            throw new InvalidOperationException("Command recording has already begun.");

        CommandBufferBeginInfo beginInfo = new()
        {
            SType = StructureType.CommandBufferBeginInfo,
        };

        if (VulkanContext.Vk.BeginCommandBuffer(_commandBuffer, &beginInfo) != Result.Success)
            throw new InvalidOperationException("Failed to begin recording command buffer.");

        _begun = true;
    }

    /// <inheritdoc/>
    public override void BeginRendering(in RenderingDescription description)
    {
        EnsureRecording();
        if (_rendering)
            throw new InvalidOperationException("Rendering has already begun.");
        if (_presentTransitionRecorded)
            throw new InvalidOperationException(
                "Rendering cannot begin after the frame target was transitioned for presentation.");

        var frame = GetFrame();
        RecordImageTransition(ImageLayout.Undefined, ImageLayout.ColorAttachmentOptimal,
            PipelineStageFlags2.TopOfPipeBit, AccessFlags2.None,
            PipelineStageFlags2.ColorAttachmentOutputBit, AccessFlags2.ColorAttachmentWriteBit);

        ClearValue clearColor = new()
        {
            Color = new ClearColorValue
            {
                Float32_0 = description.ClearColor.R,
                Float32_1 = description.ClearColor.G,
                Float32_2 = description.ClearColor.B,
                Float32_3 = description.ClearColor.A,
            },
        };

        RenderingAttachmentInfo colorAttachment = new()
        {
            SType = StructureType.RenderingAttachmentInfo,
            ImageView = frame.ImageView,
            ImageLayout = ImageLayout.ColorAttachmentOptimal,
            LoadOp = ToVulkanLoadOperation(description.LoadOperation),
            StoreOp = ToVulkanStoreOperation(description.StoreOperation),
            ClearValue = clearColor,
        };

        RenderingInfo renderingInfo = new()
        {
            SType = StructureType.RenderingInfo,
            RenderArea = new Rect2D
            {
                Offset = new Offset2D(0, 0),
                Extent = new Extent2D { Width = frame.Width, Height = frame.Height },
            },
            LayerCount = 1,
            ColorAttachmentCount = 1,
            PColorAttachments = &colorAttachment,
        };

        VulkanContext.Vk.CmdBeginRendering(_commandBuffer, &renderingInfo);
        _rendering = true;
    }

    /// <inheritdoc/>
    public override void SetViewport(float x, float y, float width, float height)
    {
        EnsureRecording();

        Viewport viewport = new()
        {
            X = x,
            Y = y,
            Width = width,
            Height = height,
            MinDepth = 0,
            MaxDepth = 1,
        };

        VulkanContext.Vk.CmdSetViewport(_commandBuffer, 0, 1, &viewport);
    }

    /// <inheritdoc/>
    public override void SetScissor(int x, int y, uint width, uint height)
    {
        EnsureRecording();

        Rect2D scissor = new()
        {
            Offset = new Offset2D(x, y),
            Extent = new Extent2D { Width = width, Height = height },
        };

        VulkanContext.Vk.CmdSetScissor(_commandBuffer, 0, 1, &scissor);
    }

    /// <inheritdoc/>
    public override void SetGraphicsPipeline(GraphicsPipeline pipeline)
    {
        EnsureRecording();
        ArgumentNullException.ThrowIfNull(pipeline);

        var vulkanPipeline = pipeline as VulkanGraphicsPipeline
                             ?? throw new InvalidOperationException(
                                 "Graphics pipeline was not created by the Vulkan backend.");

        if (vulkanPipeline.IsDestroyed)
            throw new ObjectDisposedException(nameof(VulkanGraphicsPipeline));

        VulkanContext.Vk.CmdBindPipeline(_commandBuffer, PipelineBindPoint.Graphics, vulkanPipeline.Pipeline);
        _graphicsPipeline = vulkanPipeline;
    }

    /// <inheritdoc/>
    public override void SetVertexBuffer(uint slot, Buffer buffer, ulong offset = 0)
    {
        EnsureRecording();
        ArgumentNullException.ThrowIfNull(buffer);

        var vulkanBuffer = buffer as VulkanBuffer
                           ?? throw new InvalidOperationException(
                               "Graphics buffer was not created by the Vulkan backend.");
        if (vulkanBuffer.IsDestroyed)
            throw new ObjectDisposedException(nameof(VulkanBuffer));
        if (!vulkanBuffer.Usage.HasFlag(BufferUsageFlags.VertexBufferBit))
            throw new InvalidOperationException("Buffer was not created with Vertex usage.");
        if (offset > vulkanBuffer.Size)
            throw new ArgumentOutOfRangeException(nameof(offset), "Vertex buffer offset exceeds the buffer bounds.");

        var vkBuffer = vulkanBuffer.Buffer;
        var vkOffset = offset;
        VulkanContext.Vk.CmdBindVertexBuffers(_commandBuffer, slot, 1, &vkBuffer, &vkOffset);
    }

    /// <inheritdoc/>
    public override void SetDescriptorSet(uint slot, DescriptorSet descriptorSet)
    {
        EnsureRecording();
        ArgumentNullException.ThrowIfNull(descriptorSet);

        var pipeline = _graphicsPipeline
                       ?? throw new InvalidOperationException(
                           "A graphics pipeline must be bound before binding a descriptor set.");
        var vulkanDescriptorSet = descriptorSet as VulkanDescriptorSet
                                  ?? throw new InvalidOperationException(
                                      "Descriptor set was not created by the Vulkan backend.");
        if (vulkanDescriptorSet.IsDestroyed)
            throw new ObjectDisposedException(nameof(VulkanDescriptorSet));
        if (slot >= pipeline.DescriptorSetLayouts.Count)
            throw new ArgumentOutOfRangeException(nameof(slot),
                "Descriptor set slot exceeds the graphics pipeline layout count.");
        if (!ReferenceEquals(pipeline.DescriptorSetLayouts[(int)slot], vulkanDescriptorSet.Layout))
            throw new InvalidOperationException(
                "Descriptor set layout does not match the graphics pipeline layout slot.");

        var vulkanDescriptorSetHandle = vulkanDescriptorSet.Handle;
        VulkanContext.Vk.CmdBindDescriptorSets(_commandBuffer, PipelineBindPoint.Graphics, pipeline.Layout, slot, 1,
            &vulkanDescriptorSetHandle, 0, null);
    }

    /// <inheritdoc/>
    public override void Draw(uint vertexCount, uint instanceCount = 1, uint firstVertex = 0, uint firstInstance = 0)
    {
        EnsureRecording();
        if (!_rendering)
            throw new InvalidOperationException("Draw commands must be recorded inside an active rendering operation.");

        VulkanContext.Vk.CmdDraw(_commandBuffer, vertexCount, instanceCount, firstVertex, firstInstance);
    }

    /// <inheritdoc/>
    public override void EndRendering()
    {
        EnsureRecording();
        if (!_rendering)
            throw new InvalidOperationException("Rendering has not begun.");

        VulkanContext.Vk.CmdEndRendering(_commandBuffer);
        _rendering = false;
        RecordPresentTransition(ImageLayout.ColorAttachmentOptimal,
            PipelineStageFlags2.ColorAttachmentOutputBit, AccessFlags2.ColorAttachmentWriteBit);
    }

    /// <inheritdoc/>
    public override void End()
    {
        EnsureRecording();
        if (_rendering)
            throw new InvalidOperationException("Rendering must end before command recording ends.");

        if (!_presentTransitionRecorded)
            RecordPresentTransition(ImageLayout.Undefined, PipelineStageFlags2.TopOfPipeBit, AccessFlags2.None);

        EndCommandBuffer();
    }

    /// <summary>
    /// Completes command recording for submission.
    /// </summary>
    internal void CompleteForSubmit()
    {
        EnsureUsable();

        if (!_begun)
            Begin();

        if (_rendering)
        {
            VulkanContext.Vk.CmdEndRendering(_commandBuffer);
            _rendering = false;
            RecordPresentTransition(ImageLayout.ColorAttachmentOptimal,
                PipelineStageFlags2.ColorAttachmentOutputBit, AccessFlags2.ColorAttachmentWriteBit);
        }

        if (!_presentTransitionRecorded)
            RecordPresentTransition(ImageLayout.Undefined, PipelineStageFlags2.TopOfPipeBit, AccessFlags2.None);

        if (!_ended)
            EndCommandBuffer();
    }

    /// <summary>
    /// Invalidates the command list.
    /// </summary>
    internal void Invalidate()
    {
        _valid = false;
        _frame = null;
        _commandBuffer = default;
        _graphicsPipeline = null;
    }

    private VulkanFrame GetFrame()
    {
        var frame = _frame ?? throw new InvalidOperationException("Command list is not bound to an active frame.");
        frame.EnsureValid();
        return frame;
    }

    private void EnsureUsable()
    {
        if (!_valid)
            throw new InvalidOperationException("Command list is no longer valid.");

        _ = GetFrame();
    }

    private void EnsureRecording()
    {
        EnsureUsable();
        if (!_begun)
            throw new InvalidOperationException("Command recording has not begun.");
        if (_ended)
            throw new InvalidOperationException("Command recording has already ended.");
    }

    private void EndCommandBuffer()
    {
        if (_ended)
            throw new InvalidOperationException("Command recording has already ended.");

        if (VulkanContext.Vk.EndCommandBuffer(_commandBuffer) != Result.Success)
            throw new InvalidOperationException("Failed to record command buffer.");

        _ended = true;
    }

    private void RecordPresentTransition(
        ImageLayout oldLayout,
        PipelineStageFlags2 srcStageMask,
        AccessFlags2 srcAccessMask)
    {
        RecordImageTransition(oldLayout, ImageLayout.PresentSrcKhr,
            srcStageMask, srcAccessMask, PipelineStageFlags2.BottomOfPipeBit, AccessFlags2.None);
        _presentTransitionRecorded = true;
    }

    private void RecordImageTransition(
        ImageLayout oldLayout,
        ImageLayout newLayout,
        PipelineStageFlags2 srcStageMask,
        AccessFlags2 srcAccessMask,
        PipelineStageFlags2 dstStageMask,
        AccessFlags2 dstAccessMask)
    {
        var frame = GetFrame();
        ImageMemoryBarrier2 barrier = new()
        {
            SType = StructureType.ImageMemoryBarrier2,
            SrcStageMask = srcStageMask,
            SrcAccessMask = srcAccessMask,
            DstStageMask = dstStageMask,
            DstAccessMask = dstAccessMask,
            OldLayout = oldLayout,
            NewLayout = newLayout,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = frame.Image,
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

        VulkanContext.Vk.CmdPipelineBarrier2(_commandBuffer, &dependency);
    }

    private static AttachmentLoadOp ToVulkanLoadOperation(LoadOperation operation)
    {
        return operation switch
        {
            LoadOperation.Load => AttachmentLoadOp.Load,
            LoadOperation.Clear => AttachmentLoadOp.Clear,
            LoadOperation.DontCare => AttachmentLoadOp.DontCare,
            _ => throw new ArgumentOutOfRangeException(nameof(operation), "Unsupported load operation."),
        };
    }

    private static AttachmentStoreOp ToVulkanStoreOperation(StoreOperation operation)
    {
        return operation switch
        {
            StoreOperation.Store => AttachmentStoreOp.Store,
            StoreOperation.DontCare => AttachmentStoreOp.DontCare,
            _ => throw new ArgumentOutOfRangeException(nameof(operation), "Unsupported store operation."),
        };
    }
}
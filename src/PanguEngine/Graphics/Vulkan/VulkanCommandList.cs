using Silk.NET.Vulkan;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Vulkan implementation of <see cref="CommandList"/>.
/// </summary>
internal sealed unsafe class VulkanCommandList : CommandList
{
    private CommandBuffer _commandBuffer;
    private bool _begun;
    private bool _ended;
    private bool _rendering;
    private bool _valid;
    private VulkanGraphicsPipeline? _graphicsPipeline;
    private TextureFormat[] _renderingColorFormats = [];
    private TextureFormat _renderingDepthStencilFormat;

    /// <summary>
    /// Gets whether command recording has ended.
    /// </summary>
    internal bool IsEnded => _ended;

    /// <summary>
    /// Gets whether command recording has begun.
    /// </summary>
    internal bool IsBegun => _begun;

    /// <summary>
    /// Resets this command list for command recording.
    /// </summary>
    /// <param name="commandBuffer">The command buffer to record.</param>
    internal void Reset(CommandBuffer commandBuffer)
    {
        _commandBuffer = commandBuffer;
        _begun = false;
        _ended = false;
        _rendering = false;
        _valid = true;
        _graphicsPipeline = null;
        _renderingColorFormats = [];
        _renderingDepthStencilFormat = TextureFormat.Undefined;
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
        if (description.Width == 0)
            throw new ArgumentOutOfRangeException(nameof(description),
                "Rendering width must be greater than zero.");
        if (description.Height == 0)
            throw new ArgumentOutOfRangeException(nameof(description),
                "Rendering height must be greater than zero.");

        var colorDescriptions = description.ColorAttachments;
        var colorTextures = GetColorAttachments(colorDescriptions, description.Width, description.Height);
        var renderingColorFormats = new TextureFormat[colorTextures.Length];
        for (var i = 0; i < colorTextures.Length; i++)
            renderingColorFormats[i] = colorTextures[i].Format;

        var depthStencilDescription = description.DepthStencilAttachment;
        var depthStencilTexture = depthStencilDescription.HasValue
            ? GetDepthStencilAttachment(depthStencilDescription.Value.Attachment, description.Width, description.Height)
            : null;
        var hasDepthAttachment = depthStencilTexture is not null &&
                                 VulkanMapping.HasDepthAspect(depthStencilTexture.Format);
        var hasStencilAttachment = depthStencilTexture is not null &&
                                   VulkanMapping.HasStencilAspect(depthStencilTexture.Format);
        var renderingDepthStencilFormat = depthStencilTexture?.Format ?? TextureFormat.Undefined;
        if (_graphicsPipeline is not null)
        {
            ValidateColorFormats(_graphicsPipeline, renderingColorFormats);
            ValidateDepthStencilFormat(_graphicsPipeline, renderingDepthStencilFormat);
        }

        var colorAttachments = stackalloc RenderingAttachmentInfo[colorTextures.Length];
        for (var i = 0; i < colorTextures.Length; i++)
        {
            TransitionTextureLayout(colorTextures[i], ImageLayout.ColorAttachmentOptimal,
                PipelineStageFlags2.ColorAttachmentOutputBit, AccessFlags2.ColorAttachmentWriteBit);

            var clearColor = colorDescriptions[i].ClearColor;
            colorAttachments[i] = new RenderingAttachmentInfo
            {
                SType = StructureType.RenderingAttachmentInfo,
                ImageView = colorTextures[i].ImageView,
                ImageLayout = ImageLayout.ColorAttachmentOptimal,
                LoadOp = VulkanMapping.ToVulkanLoadOperation(colorDescriptions[i].LoadOperation),
                StoreOp = VulkanMapping.ToVulkanStoreOperation(colorDescriptions[i].StoreOperation),
                ClearValue = new ClearValue
                {
                    Color = new ClearColorValue
                    {
                        Float32_0 = clearColor.R,
                        Float32_1 = clearColor.G,
                        Float32_2 = clearColor.B,
                        Float32_3 = clearColor.A,
                    },
                },
            };
        }

        RenderingAttachmentInfo depthAttachment = default;
        RenderingAttachmentInfo stencilAttachment = default;

        if (depthStencilTexture is not null)
        {
            var depthStencil = depthStencilDescription!.Value;
            TransitionTextureLayout(depthStencilTexture, ImageLayout.DepthStencilAttachmentOptimal,
                PipelineStageFlags2.EarlyFragmentTestsBit | PipelineStageFlags2.LateFragmentTestsBit,
                AccessFlags2.DepthStencilAttachmentReadBit | AccessFlags2.DepthStencilAttachmentWriteBit);

            ClearValue depthStencilClear = new()
            {
                DepthStencil = new ClearDepthStencilValue
                {
                    Depth = depthStencil.DepthClearValue,
                    Stencil = depthStencil.StencilClearValue,
                },
            };

            if (hasDepthAttachment)
            {
                depthAttachment = new RenderingAttachmentInfo
                {
                    SType = StructureType.RenderingAttachmentInfo,
                    ImageView = depthStencilTexture.ImageView,
                    ImageLayout = ImageLayout.DepthStencilAttachmentOptimal,
                    LoadOp = VulkanMapping.ToVulkanLoadOperation(depthStencil.DepthLoadOperation),
                    StoreOp = VulkanMapping.ToVulkanStoreOperation(depthStencil.DepthStoreOperation),
                    ClearValue = depthStencilClear,
                };
            }

            if (hasStencilAttachment)
            {
                stencilAttachment = new RenderingAttachmentInfo
                {
                    SType = StructureType.RenderingAttachmentInfo,
                    ImageView = depthStencilTexture.ImageView,
                    ImageLayout = ImageLayout.DepthStencilAttachmentOptimal,
                    LoadOp = VulkanMapping.ToVulkanLoadOperation(depthStencil.StencilLoadOperation),
                    StoreOp = VulkanMapping.ToVulkanStoreOperation(depthStencil.StencilStoreOperation),
                    ClearValue = depthStencilClear,
                };
            }
        }

        RenderingInfo renderingInfo = new()
        {
            SType = StructureType.RenderingInfo,
            RenderArea = new Rect2D
            {
                Offset = new Offset2D(0, 0),
                Extent = new Extent2D { Width = description.Width, Height = description.Height },
            },
            LayerCount = 1,
            ColorAttachmentCount = (uint)colorTextures.Length,
            PColorAttachments = colorAttachments,
        };

        if (hasDepthAttachment)
            renderingInfo.PDepthAttachment = &depthAttachment;
        if (hasStencilAttachment)
            renderingInfo.PStencilAttachment = &stencilAttachment;

        VulkanContext.Vk.CmdBeginRendering(_commandBuffer, &renderingInfo);
        _rendering = true;
        _renderingColorFormats = renderingColorFormats;
        _renderingDepthStencilFormat = renderingDepthStencilFormat;
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

        vulkanPipeline.ThrowIfDestroyed();
        if (_rendering)
        {
            ValidateColorFormats(vulkanPipeline, _renderingColorFormats);
            ValidateDepthStencilFormat(vulkanPipeline, _renderingDepthStencilFormat);
        }

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
        vulkanBuffer.ThrowIfDestroyed();
        if (!vulkanBuffer.Usage.HasFlag(BufferUsageFlags.VertexBufferBit))
            throw new InvalidOperationException("Buffer was not created with Vertex usage.");
        if (offset > vulkanBuffer.Size)
            throw new ArgumentOutOfRangeException(nameof(offset), "Vertex buffer offset exceeds the buffer bounds.");

        var vkBuffer = vulkanBuffer.Buffer;
        var vkOffset = offset;
        VulkanContext.Vk.CmdBindVertexBuffers(_commandBuffer, slot, 1, &vkBuffer, &vkOffset);
    }

    /// <inheritdoc/>
    public override void SetIndexBuffer(Buffer buffer, IndexFormat format, ulong offset = 0)
    {
        EnsureRecording();
        ArgumentNullException.ThrowIfNull(buffer);

        var vulkanBuffer = buffer as VulkanBuffer
                           ?? throw new InvalidOperationException(
                               "Graphics buffer was not created by the Vulkan backend.");
        vulkanBuffer.ThrowIfDestroyed();
        if (!vulkanBuffer.Usage.HasFlag(BufferUsageFlags.IndexBufferBit))
            throw new InvalidOperationException("Buffer was not created with Index usage.");
        if (offset > vulkanBuffer.Size)
            throw new ArgumentOutOfRangeException(nameof(offset), "Index buffer offset exceeds the buffer bounds.");

        VulkanContext.Vk.CmdBindIndexBuffer(_commandBuffer, vulkanBuffer.Buffer, offset,
            VulkanMapping.ToVulkanIndexType(format));
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
        vulkanDescriptorSet.ThrowIfDestroyed();
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
    public override void DrawIndexed(
        uint indexCount,
        uint instanceCount = 1,
        uint firstIndex = 0,
        int vertexOffset = 0,
        uint firstInstance = 0)
    {
        EnsureRecording();
        if (!_rendering)
            throw new InvalidOperationException("Draw commands must be recorded inside an active rendering operation.");

        VulkanContext.Vk.CmdDrawIndexed(
            _commandBuffer,
            indexCount,
            instanceCount,
            firstIndex,
            vertexOffset,
            firstInstance);
    }

    /// <inheritdoc/>
    public override void EndRendering()
    {
        EnsureRecording();
        if (!_rendering)
            throw new InvalidOperationException("Rendering has not begun.");

        VulkanContext.Vk.CmdEndRendering(_commandBuffer);
        _rendering = false;
        _renderingColorFormats = [];
        _renderingDepthStencilFormat = TextureFormat.Undefined;
    }

    /// <inheritdoc/>
    public override void PrepareForPresent(Texture colorOutput)
    {
        EnsureRecording();
        ArgumentNullException.ThrowIfNull(colorOutput);

        var swapchainOutput = colorOutput as VulkanSwapchainTexture
                              ?? throw new InvalidOperationException(
                                  "Presentation output was not created by the Vulkan backend.");
        swapchainOutput.ThrowIfDestroyed();

        TransitionTextureLayout(swapchainOutput, ImageLayout.PresentSrcKhr,
            PipelineStageFlags2.BottomOfPipeBit, AccessFlags2.None);
    }

    /// <inheritdoc/>
    public override void End()
    {
        EnsureRecording();
        if (_rendering)
            throw new InvalidOperationException("Rendering must end before command recording ends.");

        EndCommandBuffer();
    }

    /// <summary>
    /// Completes command recording for submission.
    /// </summary>
    internal void CompleteForSubmit()
    {
        EnsureUsable();

        if (!_begun)
            throw new InvalidOperationException("Command recording must begin before frame submission.");

        if (_rendering)
            throw new InvalidOperationException("Rendering must end before frame submission.");

        if (!_ended)
            EndCommandBuffer();
    }

    /// <summary>
    /// Invalidates the command list.
    /// </summary>
    internal void Invalidate()
    {
        _valid = false;
        _commandBuffer = default;
        _graphicsPipeline = null;
        _renderingColorFormats = [];
        _renderingDepthStencilFormat = TextureFormat.Undefined;
    }

    private static void ValidateDepthStencilFormat(VulkanGraphicsPipeline pipeline, TextureFormat renderingFormat)
    {
        if (pipeline.DepthStencilAttachmentFormat != renderingFormat)
            throw new InvalidOperationException(
                "Graphics pipeline depth/stencil attachment format does not match the active rendering operation.");
    }

    private static void ValidateColorFormats(VulkanGraphicsPipeline pipeline,
        ReadOnlySpan<TextureFormat> renderingFormats)
    {
        if (pipeline.ColorAttachmentFormats.Length != renderingFormats.Length)
            throw new InvalidOperationException(
                "Graphics pipeline color attachment count does not match the active rendering operation.");

        for (var i = 0; i < renderingFormats.Length; i++)
        {
            if (pipeline.ColorAttachmentFormats[i] != renderingFormats[i])
                throw new InvalidOperationException(
                    "Graphics pipeline color attachment format does not match the active rendering operation.");
        }
    }

    private IVulkanTexture[] GetColorAttachments(
        ReadOnlySpan<ColorAttachmentDescription> attachments,
        uint width,
        uint height)
    {
        if (attachments.Length == 0)
            throw new InvalidOperationException("Rendering must include at least one color attachment.");

        var result = new IVulkanTexture[attachments.Length];
        for (var i = 0; i < attachments.Length; i++)
            result[i] = GetColorAttachment(attachments[i].Attachment, width, height);
        return result;
    }

    private IVulkanTexture GetColorAttachment(Texture attachment, uint width, uint height)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        var texture = attachment as IVulkanTexture
                      ?? throw new InvalidOperationException(
                          "Color attachment was not created by the Vulkan backend.");
        ObjectDisposedException.ThrowIf(texture.IsDestroyed, attachment);
        if (texture is VulkanSwapchainTexture swapchainTexture)
        {
            if (swapchainTexture.GetLayout(0, 0) == ImageLayout.PresentSrcKhr)
                throw new InvalidOperationException(
                    "Rendering cannot begin after the frame target was transitioned for presentation.");
        }

        if (texture.Dimension != TextureDimension.Type2D)
            throw new InvalidOperationException("Color attachment must be a 2D texture.");
        if (texture.MipLevels != 1)
            throw new InvalidOperationException("Color attachment must have exactly one mip level.");
        if (texture.ArrayLayers != 1)
            throw new InvalidOperationException("Color attachment must have exactly one array layer.");
        if (texture.Width != width || texture.Height != height)
            throw new InvalidOperationException("Color attachment size must match the rendering size.");
        if (!texture.Usage.HasFlag(TextureUsage.ColorAttachment))
            throw new InvalidOperationException("Texture was not created with ColorAttachment usage.");
        if (texture.Format == TextureFormat.Undefined || VulkanMapping.IsDepthStencilFormat(texture.Format))
            throw new InvalidOperationException("Color attachment must use a color format.");
        return texture;
    }

    private static IVulkanTexture? GetDepthStencilAttachment(Texture? attachment, uint width, uint height)
    {
        if (attachment is null)
            return null;

        var texture = attachment as IVulkanTexture
                      ?? throw new InvalidOperationException(
                          "Depth/stencil attachment was not created by the Vulkan backend.");
        ObjectDisposedException.ThrowIf(texture.IsDestroyed, attachment);

        if (texture.Dimension != TextureDimension.Type2D)
            throw new InvalidOperationException("Depth/stencil attachment must be a 2D texture.");
        if (texture.MipLevels != 1)
            throw new InvalidOperationException("Depth/stencil attachment must have exactly one mip level.");
        if (texture.ArrayLayers != 1)
            throw new InvalidOperationException("Depth/stencil attachment must have exactly one array layer.");
        if (texture.Width != width || texture.Height != height)
            throw new InvalidOperationException("Depth/stencil attachment size must match the rendering size.");
        if (!texture.Usage.HasFlag(TextureUsage.DepthStencilAttachment))
            throw new InvalidOperationException("Texture was not created with DepthStencilAttachment usage.");
        if (!VulkanMapping.IsDepthStencilFormat(texture.Format))
            throw new InvalidOperationException("Depth/stencil attachment must use a depth/stencil format.");

        return texture;
    }

    /// <summary>
    /// Transitions a Vulkan texture to the requested image layout.
    /// </summary>
    /// <param name="texture">The texture to transition.</param>
    /// <param name="newLayout">The requested image layout.</param>
    /// <param name="dstStageMask">The destination pipeline stage mask.</param>
    /// <param name="dstAccessMask">The destination access mask.</param>
    private void TransitionTextureLayout(
        IVulkanTexture texture,
        ImageLayout newLayout,
        PipelineStageFlags2 dstStageMask,
        AccessFlags2 dstAccessMask)
    {
        var oldLayout = texture.GetLayout(0, 0);
        if (oldLayout == newLayout)
            return;

        VulkanBarrier.RecordImageLayoutTransition(
            _commandBuffer,
            texture.Image,
            0,
            0,
            1,
            VulkanMapping.ToVulkanImageAspect(texture.Format),
            oldLayout,
            newLayout,
            VulkanBarrier.GetStageForLayout(oldLayout),
            VulkanBarrier.GetAccessForLayout(oldLayout),
            dstStageMask,
            dstAccessMask);
        texture.SetLayout(0, 0, newLayout);
    }

    private void EnsureUsable()
    {
        if (!_valid)
            throw new InvalidOperationException("Command list is no longer valid.");
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
}
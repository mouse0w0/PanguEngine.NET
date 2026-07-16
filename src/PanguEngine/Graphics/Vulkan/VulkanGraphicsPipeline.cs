using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using VKDescriptorSetLayout = Silk.NET.Vulkan.DescriptorSetLayout;
using VkPipeline = Silk.NET.Vulkan.Pipeline;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Vulkan implementation of <see cref="GraphicsPipeline"/>.
/// </summary>
internal sealed unsafe class VulkanGraphicsPipeline : GraphicsPipeline
{
    /// <summary>
    /// Creates a Vulkan graphics pipeline with no descriptor set layouts.
    /// </summary>
    /// <param name="description">The graphics pipeline description.</param>
    public VulkanGraphicsPipeline(in GraphicsPipelineDescription description)
        : this(description, GetVulkanDescriptorSetLayouts(description))
    {
    }

    /// <summary>
    /// Creates a Vulkan graphics pipeline with the given descriptor set layouts.
    /// </summary>
    /// <param name="description">The graphics pipeline description.</param>
    /// <param name="descriptorSetLayouts">The Vulkan descriptor set layouts used by the pipeline layout.</param>
    internal VulkanGraphicsPipeline(
        in GraphicsPipelineDescription description,
        ReadOnlySpan<VKDescriptorSetLayout> descriptorSetLayouts)
    {
        DescriptorSetLayouts = GetDescriptorSetLayouts(description);
        ColorAttachmentFormats = description.ColorAttachmentFormats.ToArray();
        DepthStencilAttachmentFormat = description.DepthStencilAttachmentFormat;
        CreatePipeline(description, descriptorSetLayouts);
    }

    /// <summary>
    /// Gets the Vulkan graphics pipeline handle.
    /// </summary>
    internal VkPipeline Pipeline { get; private set; }

    /// <summary>
    /// Gets the Vulkan pipeline layout handle.
    /// </summary>
    internal PipelineLayout Layout { get; private set; }

    /// <summary>
    /// Gets the depth/stencil attachment format used by this pipeline.
    /// </summary>
    internal TextureFormat DepthStencilAttachmentFormat { get; }

    /// <summary>
    /// Gets the color attachment formats used by this pipeline.
    /// </summary>
    internal TextureFormat[] ColorAttachmentFormats { get; }

    /// <summary>
    /// Gets the descriptor set layouts used by this pipeline.
    /// </summary>
    internal IReadOnlyList<VulkanDescriptorSetLayout> DescriptorSetLayouts { get; }

    /// <inheritdoc/>
    public override void Destroy()
    {
        if (IsDestroyed)
            return;
        MarkDestroyed();

        var pipeline = Pipeline;
        var layout = Layout;
        Pipeline = default;
        Layout = default;
        if (pipeline.Handle == 0 && layout.Handle == 0)
            return;

        var retireValue = VulkanContext.GlobalTimelineValue + VulkanContext.MaxFramesInFlight;
        VulkanDeletionQueue.Enqueue(retireValue, () =>
        {
            if (pipeline.Handle != 0)
                VulkanContext.Vk.DestroyPipeline(VulkanContext.Device, pipeline, null);
            if (layout.Handle != 0)
                VulkanContext.Vk.DestroyPipelineLayout(VulkanContext.Device, layout, null);
        });
    }

    private void CreatePipeline(
        in GraphicsPipelineDescription description,
        ReadOnlySpan<VKDescriptorSetLayout> descriptorSetLayouts)
    {
        var shaders = description.Shaders;
        var stageInfos = new PipelineShaderStageCreateInfo[shaders.Length];
        var entryPointPointers = new nint[shaders.Length];

        try
        {
            for (var i = 0; i < shaders.Length; i++)
            {
                var shader = (VulkanShader)shaders[i];
                entryPointPointers[i] = SilkMarshal.StringToPtr(shader.EntryPoint);
                stageInfos[i] = new PipelineShaderStageCreateInfo
                {
                    SType = StructureType.PipelineShaderStageCreateInfo,
                    Stage = VulkanMapping.ToVulkanShaderStageFlags(shader.Stage),
                    Module = shader.Module,
                    PName = (byte*)entryPointPointers[i]
                };
            }

            CreatePipeline(description, descriptorSetLayouts, stageInfos);
        }
        finally
        {
            foreach (var pointer in entryPointPointers)
            {
                if (pointer != 0)
                    SilkMarshal.Free(pointer);
            }
        }
    }

    private void CreatePipeline(
        in GraphicsPipelineDescription description,
        ReadOnlySpan<VKDescriptorSetLayout> descriptorSetLayouts,
        PipelineShaderStageCreateInfo[] stageInfos)
    {
        var bufferLayouts = description.VertexInput.Buffers;
        var attributes = description.VertexInput.Attributes;
        var bindingDescriptions = stackalloc VertexInputBindingDescription[bufferLayouts.Length];
        var attributeDescriptions = stackalloc VertexInputAttributeDescription[attributes.Length];

        for (var i = 0; i < bufferLayouts.Length; i++)
        {
            bindingDescriptions[i] = new VertexInputBindingDescription
            {
                Binding = bufferLayouts[i].Binding,
                Stride = bufferLayouts[i].Stride,
                InputRate = VulkanMapping.ToVulkanVertexInputRate(bufferLayouts[i].InputRate)
            };
        }

        for (var i = 0; i < attributes.Length; i++)
        {
            attributeDescriptions[i] = new VertexInputAttributeDescription
            {
                Location = attributes[i].Location,
                Binding = attributes[i].Binding,
                Format = VulkanMapping.ToVulkanVertexAttributeFormat(attributes[i].Format),
                Offset = attributes[i].Offset
            };
        }

        PipelineVertexInputStateCreateInfo vertexInputInfo = new()
        {
            SType = StructureType.PipelineVertexInputStateCreateInfo,
            VertexBindingDescriptionCount = (uint)bufferLayouts.Length,
            PVertexBindingDescriptions = bufferLayouts.Length == 0 ? null : bindingDescriptions,
            VertexAttributeDescriptionCount = (uint)attributes.Length,
            PVertexAttributeDescriptions = attributes.Length == 0 ? null : attributeDescriptions
        };

        PipelineInputAssemblyStateCreateInfo inputAssembly = new()
        {
            SType = StructureType.PipelineInputAssemblyStateCreateInfo,
            Topology = VulkanMapping.ToVulkanPrimitiveTopology(description.Topology),
            PrimitiveRestartEnable = false
        };

        Viewport viewport = new() { X = 0, Y = 0, Width = 1, Height = 1, MinDepth = 0, MaxDepth = 1 };
        Rect2D scissor = new() { Offset = new Offset2D(0, 0), Extent = new Extent2D(1, 1) };
        PipelineViewportStateCreateInfo viewportState = new()
        {
            SType = StructureType.PipelineViewportStateCreateInfo,
            ViewportCount = 1,
            PViewports = description.DynamicViewport ? null : &viewport,
            ScissorCount = 1,
            PScissors = description.DynamicScissor ? null : &scissor
        };

        var dynamicStateCount = (description.DynamicViewport ? 1 : 0) + (description.DynamicScissor ? 1 : 0);
        var dynamicStates = stackalloc DynamicState[dynamicStateCount];
        var dynamicStateIndex = 0;
        if (description.DynamicViewport)
            dynamicStates[dynamicStateIndex++] = DynamicState.Viewport;
        if (description.DynamicScissor)
            dynamicStates[dynamicStateIndex] = DynamicState.Scissor;

        PipelineDynamicStateCreateInfo dynamicState = new()
        {
            SType = StructureType.PipelineDynamicStateCreateInfo,
            DynamicStateCount = (uint)dynamicStateCount,
            PDynamicStates = dynamicStateCount == 0 ? null : dynamicStates
        };

        PipelineRasterizationStateCreateInfo rasterizer = new()
        {
            SType = StructureType.PipelineRasterizationStateCreateInfo,
            DepthClampEnable = false,
            RasterizerDiscardEnable = false,
            PolygonMode = PolygonMode.Fill,
            LineWidth = description.Rasterizer.LineWidth == 0 ? 1 : description.Rasterizer.LineWidth,
            CullMode = VulkanMapping.ToVulkanCullMode(description.Rasterizer.CullMode),
            FrontFace = VulkanMapping.ToVulkanFrontFace(description.Rasterizer.FrontFace),
            DepthBiasEnable = false
        };

        PipelineMultisampleStateCreateInfo multisampling = new()
        {
            SType = StructureType.PipelineMultisampleStateCreateInfo,
            SampleShadingEnable = false,
            RasterizationSamples = SampleCountFlags.Count1Bit
        };

        var colorAttachmentFormats = description.ColorAttachmentFormats;
        var vkColorFormats = stackalloc Format[colorAttachmentFormats.Length];
        var colorBlendAttachments = stackalloc PipelineColorBlendAttachmentState[colorAttachmentFormats.Length];
        for (var i = 0; i < colorAttachmentFormats.Length; i++)
        {
            vkColorFormats[i] = VulkanMapping.ToVulkanFormat(colorAttachmentFormats[i]);
            colorBlendAttachments[i] = CreateColorBlendAttachment(description.ColorBlend);
        }

        PipelineColorBlendStateCreateInfo colorBlending = new()
        {
            SType = StructureType.PipelineColorBlendStateCreateInfo,
            LogicOpEnable = false,
            LogicOp = LogicOp.Copy,
            AttachmentCount = (uint)colorAttachmentFormats.Length,
            PAttachments = colorBlendAttachments
        };

        var stencilTestEnabled = description.DepthStencil.StencilTestEnabled;
        PipelineDepthStencilStateCreateInfo depthStencil = new()
        {
            SType = StructureType.PipelineDepthStencilStateCreateInfo,
            DepthTestEnable = description.DepthStencil.DepthTestEnabled,
            DepthWriteEnable = description.DepthStencil.DepthWriteEnabled,
            DepthCompareOp = description.DepthStencil.DepthTestEnabled
                ? VulkanMapping.ToVulkanCompareOp(description.DepthStencil.DepthCompareOperation)
                : CompareOp.Always,
            DepthBoundsTestEnable = false,
            StencilTestEnable = stencilTestEnabled,
            Front = stencilTestEnabled
                ? VulkanMapping.ToVulkanStencilOpState(description.DepthStencil.FrontFace)
                : CreateDisabledStencilOpState(),
            Back = stencilTestEnabled
                ? VulkanMapping.ToVulkanStencilOpState(description.DepthStencil.BackFace)
                : CreateDisabledStencilOpState(),
            MinDepthBounds = 0,
            MaxDepthBounds = 1
        };

        var pushConstantRangeDescriptions = description.PushConstantRanges;
        var pushConstantRanges = stackalloc PushConstantRange[pushConstantRangeDescriptions.Length];
        for (var i = 0; i < pushConstantRangeDescriptions.Length; i++)
            pushConstantRanges[i] = VulkanMapping.ToVulkanPushConstantRange(pushConstantRangeDescriptions[i]);

        fixed (VKDescriptorSetLayout* descriptorLayouts = descriptorSetLayouts)
        fixed (PipelineShaderStageCreateInfo* stages = stageInfos)
        {
            PipelineLayoutCreateInfo pipelineLayoutInfo = new()
            {
                SType = StructureType.PipelineLayoutCreateInfo,
                SetLayoutCount = (uint)descriptorSetLayouts.Length,
                PSetLayouts = descriptorSetLayouts.Length == 0 ? null : descriptorLayouts,
                PushConstantRangeCount = (uint)pushConstantRangeDescriptions.Length,
                PPushConstantRanges = pushConstantRangeDescriptions.Length == 0 ? null : pushConstantRanges
            };

            if (VulkanContext.Vk.CreatePipelineLayout(VulkanContext.Device, in pipelineLayoutInfo, null,
                    out var pipelineLayout) != Result.Success)
                throw new InvalidOperationException("Failed to create pipeline layout.");

            Layout = pipelineLayout;

            var depthFormat = Format.Undefined;
            var stencilFormat = Format.Undefined;
            if (description.DepthStencilAttachmentFormat != TextureFormat.Undefined)
            {
                var depthStencilFormat = description.DepthStencilAttachmentFormat;
                var vkDepthStencilFormat = VulkanMapping.ToVulkanFormat(depthStencilFormat);
                if (VulkanMapping.HasDepthAspect(depthStencilFormat))
                    depthFormat = vkDepthStencilFormat;
                if (VulkanMapping.HasStencilAspect(depthStencilFormat))
                    stencilFormat = vkDepthStencilFormat;
            }

            PipelineRenderingCreateInfo renderingCreateInfo = new()
            {
                SType = StructureType.PipelineRenderingCreateInfo,
                ColorAttachmentCount = (uint)colorAttachmentFormats.Length,
                PColorAttachmentFormats = vkColorFormats,
                DepthAttachmentFormat = depthFormat,
                StencilAttachmentFormat = stencilFormat
            };

            GraphicsPipelineCreateInfo pipelineInfo = new()
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                PNext = &renderingCreateInfo,
                StageCount = (uint)stageInfos.Length,
                PStages = stages,
                PVertexInputState = &vertexInputInfo,
                PInputAssemblyState = &inputAssembly,
                PViewportState = &viewportState,
                PRasterizationState = &rasterizer,
                PMultisampleState = &multisampling,
                PDepthStencilState = &depthStencil,
                PColorBlendState = &colorBlending,
                PDynamicState = dynamicStateCount == 0 ? null : &dynamicState,
                Layout = Layout,
                BasePipelineHandle = default
            };

            if (VulkanContext.Vk.CreateGraphicsPipelines(VulkanContext.Device, default, 1, in pipelineInfo, null,
                    out var pipeline) != Result.Success)
            {
                VulkanContext.Vk.DestroyPipelineLayout(VulkanContext.Device, Layout, null);
                Layout = default;
                throw new InvalidOperationException("Failed to create graphics pipeline.");
            }

            Pipeline = pipeline;
        }
    }

    private static PipelineColorBlendAttachmentState CreateColorBlendAttachment(ColorBlendDescription description)
    {
        PipelineColorBlendAttachmentState result = new()
        {
            ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit |
                             ColorComponentFlags.ABit,
            BlendEnable = description.AlphaBlend
        };

        if (!description.AlphaBlend)
            return result;

        result.SrcColorBlendFactor = BlendFactor.SrcAlpha;
        result.DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha;
        result.ColorBlendOp = BlendOp.Add;
        result.SrcAlphaBlendFactor = BlendFactor.One;
        result.DstAlphaBlendFactor = BlendFactor.OneMinusSrcAlpha;
        result.AlphaBlendOp = BlendOp.Add;
        return result;
    }

    private static StencilOpState CreateDisabledStencilOpState()
    {
        return new StencilOpState
        {
            FailOp = StencilOp.Keep,
            PassOp = StencilOp.Keep,
            DepthFailOp = StencilOp.Keep,
            CompareOp = CompareOp.Always,
            CompareMask = 0xff,
            WriteMask = 0xff
        };
    }

    private static VulkanDescriptorSetLayout[] GetDescriptorSetLayouts(in GraphicsPipelineDescription description)
    {
        var layouts = description.DescriptorSetLayouts;
        if (layouts.Length == 0)
            return [];

        var result = new VulkanDescriptorSetLayout[layouts.Length];
        for (var i = 0; i < layouts.Length; i++)
        {
            result[i] = layouts[i] as VulkanDescriptorSetLayout
                        ?? throw new InvalidOperationException(
                            "Descriptor set layout was not created by the Vulkan backend.");
        }

        return result;
    }

    private static VKDescriptorSetLayout[] GetVulkanDescriptorSetLayouts(
        in GraphicsPipelineDescription description)
    {
        var layouts = description.DescriptorSetLayouts;
        if (layouts.Length == 0)
            return [];

        var result = new VKDescriptorSetLayout[layouts.Length];
        for (var i = 0; i < layouts.Length; i++)
        {
            var layout = layouts[i] as VulkanDescriptorSetLayout
                         ?? throw new InvalidOperationException(
                             "Descriptor set layout was not created by the Vulkan backend.");
            result[i] = layout.DescriptorSetLayout;
        }

        return result;
    }
}
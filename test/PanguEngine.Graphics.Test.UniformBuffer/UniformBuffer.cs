using System.Diagnostics;
using System.Runtime.InteropServices;
using PanguEngine.Graphics.Vulkan;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Vma;
using VkFrontFace = Silk.NET.Vulkan.FrontFace;
using VkPrimitiveTopology = Silk.NET.Vulkan.PrimitiveTopology;
using VkVertexInputRate = Silk.NET.Vulkan.VertexInputRate;

namespace PanguEngine.Graphics.Test.UniformBuffer;

internal static class UniformBuffer
{
    private static void Main()
    {
        new VulkanTestApp(new UniformBufferScene()).Run();
    }
}

internal sealed unsafe class UniformBufferScene : IVulkanTestScene
{
    private readonly Vertex[] _vertices =
    [
        new(0.0f, -0.45f, 1, 1, 1),
        new(0.45f, 0.45f, 1, 1, 1),
        new(-0.45f, 0.45f, 1, 1, 1),
    ];

    private ShaderModule _vertShaderModule;
    private ShaderModule _fragShaderModule;
    private DescriptorSetLayout _descriptorSetLayout;
    private DescriptorPool _descriptorPool;
    private PipelineLayout _pipelineLayout;
    private Pipeline _pipeline;
    private DescriptorSet[]? _descriptorSets;
    private VulkanBuffer? _vertexBuffer;
    private VulkanUniformBuffer? _uniformBuffer;
    private VulkanUploader.UploadHandle? _uploadHandle;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private uint _frameCount;

    public string Name => "UniformBuffer";

    public void Initialize(VulkanWindow window)
    {
        CreateVertexBuffer();
        _uniformBuffer = new VulkanUniformBuffer((ulong)Marshal.SizeOf<FrameUniform>());
        CreateDescriptorSetLayout();
        CreateDescriptorSets();
        CreateShaders();
        CreatePipeline(window.ImageFormat);
    }

    public void Record(CommandBuffer commandBuffer, ImageView targetImageView, Extent2D extent, Format imageFormat)
    {
        if (_uploadHandle is { IsCompleted: false })
            throw new InvalidOperationException(
                "Vertex buffer upload did not complete after flushing pending uploads.");

        var frameIndex = _frameCount % VulkanContext.MaxFramesInFlight;
        var descriptorIndex = checked((int)frameIndex);
        WriteFrameUniform(frameIndex);
        _frameCount++;

        ClearValue clearColor = new()
        {
            Color = new ClearColorValue { Float32_0 = 0.01f, Float32_1 = 0.012f, Float32_2 = 0.018f, Float32_3 = 1 },
        };

        RenderingAttachmentInfo colorAttachment = new()
        {
            SType = StructureType.RenderingAttachmentInfo,
            ImageView = targetImageView,
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
        VulkanContext.Vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, _pipeline);

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

        var vertexBuffer = _vertexBuffer!.Buffer;
        var offsets = stackalloc ulong[] { 0 };
        VulkanContext.Vk.CmdBindVertexBuffers(commandBuffer, 0, 1, &vertexBuffer, offsets);

        var descriptorSet = _descriptorSets![descriptorIndex];
        VulkanContext.Vk.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Graphics, _pipelineLayout, 0, 1,
            &descriptorSet, 0, null);

        VulkanContext.Vk.CmdDraw(commandBuffer, (uint)_vertices.Length, 1, 0, 0);
        VulkanContext.Vk.CmdEndRendering(commandBuffer);
    }

    public void Destroy()
    {
        if (_pipeline.Handle != 0)
            VulkanContext.Vk.DestroyPipeline(VulkanContext.Device, _pipeline, null);
        if (_pipelineLayout.Handle != 0)
            VulkanContext.Vk.DestroyPipelineLayout(VulkanContext.Device, _pipelineLayout, null);
        if (_descriptorPool.Handle != 0)
            VulkanContext.Vk.DestroyDescriptorPool(VulkanContext.Device, _descriptorPool, null);
        if (_descriptorSetLayout.Handle != 0)
            VulkanContext.Vk.DestroyDescriptorSetLayout(VulkanContext.Device, _descriptorSetLayout, null);
        if (_vertShaderModule.Handle != 0)
            VulkanTestShader.DestroyShaderModule(_vertShaderModule);
        if (_fragShaderModule.Handle != 0)
            VulkanTestShader.DestroyShaderModule(_fragShaderModule);

        _uniformBuffer?.Destroy();
        _vertexBuffer?.Destroy();
    }

    private void CreateVertexBuffer()
    {
        var size = (ulong)(Marshal.SizeOf<Vertex>() * _vertices.Length);

        BufferCreateInfo bufferInfo = new()
        {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = BufferUsageFlags.TransferDstBit | BufferUsageFlags.VertexBufferBit,
            SharingMode = SharingMode.Exclusive,
        };

        AllocationCreateInfo allocInfo = new()
        {
            Usage = MemoryUsage.Auto,
            PreferredFlags = MemoryPropertyFlags.DeviceLocalBit,
        };

        _vertexBuffer = VulkanAllocator.CreateBuffer(in bufferInfo, in allocInfo);
        _uploadHandle = VulkanUploader.EnqueueBufferUpload(_vertexBuffer, _vertices);
    }

    private void CreateDescriptorSetLayout()
    {
        DescriptorSetLayoutBinding binding = new()
        {
            Binding = 0,
            DescriptorType = DescriptorType.UniformBuffer,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.VertexBit,
        };

        DescriptorSetLayoutCreateInfo layoutInfo = new()
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = 1,
            PBindings = &binding,
        };

        if (VulkanContext.Vk.CreateDescriptorSetLayout(VulkanContext.Device, in layoutInfo, null,
                out _descriptorSetLayout) != Result.Success)
            throw new InvalidOperationException("Failed to create descriptor set layout.");
    }

    private void CreateDescriptorSets()
    {
        DescriptorPoolSize poolSize = new()
        {
            Type = DescriptorType.UniformBuffer,
            DescriptorCount = VulkanContext.MaxFramesInFlight,
        };

        DescriptorPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            MaxSets = VulkanContext.MaxFramesInFlight,
            PoolSizeCount = 1,
            PPoolSizes = &poolSize,
        };

        if (VulkanContext.Vk.CreateDescriptorPool(VulkanContext.Device, in poolInfo, null, out _descriptorPool) !=
            Result.Success)
            throw new InvalidOperationException("Failed to create descriptor pool.");

        var frameCount = checked((int)VulkanContext.MaxFramesInFlight);
        _descriptorSets = new DescriptorSet[frameCount];
        var layouts = stackalloc DescriptorSetLayout[frameCount];
        for (var i = 0; i < frameCount; i++)
            layouts[i] = _descriptorSetLayout;

        fixed (DescriptorSet* descriptorSets = _descriptorSets)
        {
            DescriptorSetAllocateInfo allocInfo = new()
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = _descriptorPool,
                DescriptorSetCount = VulkanContext.MaxFramesInFlight,
                PSetLayouts = layouts,
            };

            if (VulkanContext.Vk.AllocateDescriptorSets(VulkanContext.Device, in allocInfo, descriptorSets) !=
                Result.Success)
                throw new InvalidOperationException("Failed to allocate descriptor sets.");

            for (var i = 0U; i < VulkanContext.MaxFramesInFlight; i++)
            {
                DescriptorBufferInfo bufferInfo = new()
                {
                    Buffer = _uniformBuffer!.Buffer.Buffer,
                    Offset = _uniformBuffer.GetOffset(i),
                    Range = (ulong)Marshal.SizeOf<FrameUniform>(),
                };

                WriteDescriptorSet descriptorWrite = new()
                {
                    SType = StructureType.WriteDescriptorSet,
                    DstSet = descriptorSets[i],
                    DstBinding = 0,
                    DstArrayElement = 0,
                    DescriptorType = DescriptorType.UniformBuffer,
                    DescriptorCount = 1,
                    PBufferInfo = &bufferInfo,
                };

                VulkanContext.Vk.UpdateDescriptorSets(VulkanContext.Device, 1, in descriptorWrite, 0, null);
            }
        }
    }

    private void CreateShaders()
    {
        var basePath = AppContext.BaseDirectory;
        var vertPath = Path.Combine(basePath, "Shaders", "uniform_buffer.vert");
        var fragPath = Path.Combine(basePath, "Shaders", "uniform_buffer.frag");

        var vertSource = File.ReadAllText(vertPath);
        var fragSource = File.ReadAllText(fragPath);

        _vertShaderModule = VulkanTestShader.CreateVertexShader(vertSource, "uniform_buffer.vert");
        _fragShaderModule = VulkanTestShader.CreateFragmentShader(fragSource, "uniform_buffer.frag");
    }

    private void CreatePipeline(Format imageFormat)
    {
        PipelineShaderStageCreateInfo vertShaderStageInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.VertexBit,
            Module = _vertShaderModule,
            PName = (byte*)SilkMarshal.StringToPtr("main"),
        };

        PipelineShaderStageCreateInfo fragShaderStageInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.FragmentBit,
            Module = _fragShaderModule,
            PName = (byte*)SilkMarshal.StringToPtr("main"),
        };

        try
        {
            var shaderStages = stackalloc[]
            {
                vertShaderStageInfo,
                fragShaderStageInfo,
            };

            VertexInputBindingDescription bindingDescription = new()
            {
                Binding = 0,
                Stride = (uint)Marshal.SizeOf<Vertex>(),
                InputRate = VkVertexInputRate.Vertex,
            };

            var attributeDescriptions = stackalloc VertexInputAttributeDescription[]
            {
                new()
                {
                    Binding = 0,
                    Location = 0,
                    Format = Format.R32G32Sfloat,
                    Offset = 0,
                },
                new()
                {
                    Binding = 0,
                    Location = 1,
                    Format = Format.R32G32B32Sfloat,
                    Offset = 8,
                },
            };

            PipelineVertexInputStateCreateInfo vertexInputInfo = new()
            {
                SType = StructureType.PipelineVertexInputStateCreateInfo,
                VertexBindingDescriptionCount = 1,
                PVertexBindingDescriptions = &bindingDescription,
                VertexAttributeDescriptionCount = 2,
                PVertexAttributeDescriptions = attributeDescriptions,
            };

            PipelineInputAssemblyStateCreateInfo inputAssembly = new()
            {
                SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                Topology = VkPrimitiveTopology.TriangleList,
                PrimitiveRestartEnable = false,
            };

            PipelineViewportStateCreateInfo viewportState = new()
            {
                SType = StructureType.PipelineViewportStateCreateInfo,
                ViewportCount = 1,
                ScissorCount = 1,
            };

            var dynamicStates = stackalloc[] { DynamicState.Viewport, DynamicState.Scissor };
            PipelineDynamicStateCreateInfo dynamicState = new()
            {
                SType = StructureType.PipelineDynamicStateCreateInfo,
                DynamicStateCount = 2,
                PDynamicStates = dynamicStates,
            };

            PipelineRasterizationStateCreateInfo rasterizer = new()
            {
                SType = StructureType.PipelineRasterizationStateCreateInfo,
                DepthClampEnable = false,
                RasterizerDiscardEnable = false,
                PolygonMode = PolygonMode.Fill,
                LineWidth = 1,
                CullMode = CullModeFlags.BackBit,
                FrontFace = VkFrontFace.Clockwise,
                DepthBiasEnable = false,
            };

            PipelineMultisampleStateCreateInfo multisampling = new()
            {
                SType = StructureType.PipelineMultisampleStateCreateInfo,
                SampleShadingEnable = false,
                RasterizationSamples = SampleCountFlags.Count1Bit,
            };

            PipelineColorBlendAttachmentState colorBlendAttachment = new()
            {
                ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit |
                                 ColorComponentFlags.ABit,
                BlendEnable = false,
            };

            PipelineColorBlendStateCreateInfo colorBlending = new()
            {
                SType = StructureType.PipelineColorBlendStateCreateInfo,
                LogicOpEnable = false,
                LogicOp = LogicOp.Copy,
                AttachmentCount = 1,
                PAttachments = &colorBlendAttachment,
            };

            var descriptorSetLayout = _descriptorSetLayout;
            PipelineLayoutCreateInfo pipelineLayoutInfo = new()
            {
                SType = StructureType.PipelineLayoutCreateInfo,
                SetLayoutCount = 1,
                PSetLayouts = &descriptorSetLayout,
                PushConstantRangeCount = 0,
            };

            if (VulkanContext.Vk.CreatePipelineLayout(VulkanContext.Device, in pipelineLayoutInfo, null,
                    out _pipelineLayout) != Result.Success)
                throw new InvalidOperationException("Failed to create pipeline layout.");

            PipelineRenderingCreateInfo renderingCreateInfo = new()
            {
                SType = StructureType.PipelineRenderingCreateInfo,
                ColorAttachmentCount = 1,
                PColorAttachmentFormats = &imageFormat,
            };

            GraphicsPipelineCreateInfo pipelineInfo = new()
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                PNext = &renderingCreateInfo,
                StageCount = 2,
                PStages = shaderStages,
                PVertexInputState = &vertexInputInfo,
                PInputAssemblyState = &inputAssembly,
                PViewportState = &viewportState,
                PRasterizationState = &rasterizer,
                PMultisampleState = &multisampling,
                PColorBlendState = &colorBlending,
                PDynamicState = &dynamicState,
                Layout = _pipelineLayout,
                BasePipelineHandle = default,
            };

            if (VulkanContext.Vk.CreateGraphicsPipelines(VulkanContext.Device, default, 1, in pipelineInfo, null,
                    out _pipeline) != Result.Success)
                throw new InvalidOperationException("Failed to create graphics pipeline.");
        }
        finally
        {
            SilkMarshal.Free((nint)vertShaderStageInfo.PName);
            SilkMarshal.Free((nint)fragShaderStageInfo.PName);
        }
    }

    private void WriteFrameUniform(uint frameIndex)
    {
        var time = (float)_stopwatch.Elapsed.TotalSeconds;
        var uniform = _uniformBuffer!.GetMappedData<FrameUniform>(frameIndex);
        uniform->TintR = 0.5f + MathF.Sin(time) * 0.5f;
        uniform->TintG = 0.5f + MathF.Sin(time + 2.0943952f) * 0.5f;
        uniform->TintB = 0.5f + MathF.Sin(time + 4.1887903f) * 0.5f;
        uniform->TintA = 1;
        uniform->OffsetX = MathF.Sin(time) * 0.25f;
        uniform->OffsetY = 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct Vertex(float X, float Y, float R, float G, float B);

    [StructLayout(LayoutKind.Sequential)]
    private struct FrameUniform
    {
        public float TintR;
        public float TintG;
        public float TintB;
        public float TintA;
        public float OffsetX;
        public float OffsetY;
        public float PaddingX;
        public float PaddingY;
    }
}
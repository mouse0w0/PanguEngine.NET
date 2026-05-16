using System.Diagnostics;
using System.Runtime.InteropServices;
using PanguEngine.Graphics.Vulkan;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Vma;

namespace PanguEngine.Graphics.Test.BufferLifecycle;

internal static class BufferLifecycle
{
    private static void Main()
    {
        new VulkanTestApp(new BufferLifecycleScene()).Run();
    }
}

internal sealed unsafe class BufferLifecycleScene : IVulkanTestScene
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    private ShaderModule _vertShaderModule;
    private ShaderModule _fragShaderModule;
    private PipelineLayout _pipelineLayout;
    private Pipeline _pipeline;
    private Mesh _mesh = null!;

    public string Name => "BufferLifecycle";

    public void Initialize(VulkanWindow window)
    {
        CreateShaders();
        CreatePipeline(window.ImageFormat);
    }

    public void PrepareFrame()
    {
        _mesh = CreateMesh();
    }

    public void Record(CommandBuffer commandBuffer, ImageView targetImageView, Extent2D extent, Format imageFormat)
    {
        ClearValue clearColor = new()
        {
            Color = new ClearColorValue { Float32_0 = 0.008f, Float32_1 = 0.01f, Float32_2 = 0.016f, Float32_3 = 1 },
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

        if (!_mesh.UploadHandle.IsCompleted)
            throw new InvalidOperationException("Mesh buffer upload did not complete after flushing pending uploads.");

        var vertexBuffer = _mesh.Buffer.Buffer;
        var offsets = stackalloc ulong[] { 0 };
        VulkanContext.Vk.CmdBindVertexBuffers(commandBuffer, 0, 1, &vertexBuffer, offsets);
        VulkanContext.Vk.CmdDraw(commandBuffer, _mesh.VertexCount, 1, 0, 0);

        VulkanContext.Vk.CmdEndRendering(commandBuffer);

        _mesh.Buffer.Destroy();
    }

    public void Destroy()
    {
        if (_pipeline.Handle != 0)
            VulkanContext.Vk.DestroyPipeline(VulkanContext.Device, _pipeline, null);
        if (_pipelineLayout.Handle != 0)
            VulkanContext.Vk.DestroyPipelineLayout(VulkanContext.Device, _pipelineLayout, null);
        if (_vertShaderModule.Handle != 0)
            VulkanShader.DestroyShaderModule(_vertShaderModule);
        if (_fragShaderModule.Handle != 0)
            VulkanShader.DestroyShaderModule(_fragShaderModule);
    }

    private Mesh CreateMesh()
    {
        var vertices = CreateVertices();
        var size = (ulong)(Marshal.SizeOf<Vertex>() * vertices.Length);

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

        var buffer = VulkanAllocator.CreateBuffer(in bufferInfo, in allocInfo);
        var uploadHandle = VulkanUploader.EnqueueBufferUpload(buffer, vertices);
        return new Mesh(buffer, uploadHandle, (uint)vertices.Length);
    }

    private Vertex[] CreateVertices()
    {
        var time = (float)_stopwatch.Elapsed.TotalSeconds;
        var x = MathF.Sin(time * 1.6f) * 0.32f;
        var y = MathF.Cos(time * 1.2f) * 0.18f;
        var radius = 0.22f + MathF.Sin(time * 2.1f) * 0.06f;
        var r = 0.5f + MathF.Sin(time) * 0.5f;
        var g = 0.5f + MathF.Sin(time + 2.0943952f) * 0.5f;
        var b = 0.5f + MathF.Sin(time + 4.1887903f) * 0.5f;

        return
        [
            new(x, y - radius, r, g, b),
            new(x + radius, y + radius, b, r, g),
            new(x - radius, y + radius, g, b, r),
        ];
    }

    private void CreateShaders()
    {
        var basePath = AppContext.BaseDirectory;
        var vertPath = Path.Combine(basePath, "Shaders", "buffer_lifecycle.vert");
        var fragPath = Path.Combine(basePath, "Shaders", "buffer_lifecycle.frag");

        var vertSource = File.ReadAllText(vertPath);
        var fragSource = File.ReadAllText(fragPath);

        _vertShaderModule = VulkanShader.CreateVertexShader(vertSource, "buffer_lifecycle.vert");
        _fragShaderModule = VulkanShader.CreateFragmentShader(fragSource, "buffer_lifecycle.frag");
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
                InputRate = VertexInputRate.Vertex,
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
                Topology = PrimitiveTopology.TriangleList,
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
                FrontFace = FrontFace.Clockwise,
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

            PipelineLayoutCreateInfo pipelineLayoutInfo = new()
            {
                SType = StructureType.PipelineLayoutCreateInfo,
                SetLayoutCount = 0,
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

    private sealed record Mesh(
        VulkanBuffer Buffer,
        VulkanUploader.UploadHandle UploadHandle,
        uint VertexCount);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct Vertex(float X, float Y, float R, float G, float B);
}
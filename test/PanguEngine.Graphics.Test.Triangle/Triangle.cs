using PanguEngine.Graphics.Vulkan;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace PanguEngine.Graphics.Test.Triangle;

internal static class Triangle
{
    private static void Main()
    {
        new VulkanTestApp(new TriangleScene()).Run();
    }
}

internal sealed unsafe class TriangleScene : IVulkanTestScene
{
    private ShaderModule _vertShaderModule;
    private ShaderModule _fragShaderModule;
    private PipelineLayout _pipelineLayout;
    private Pipeline _pipeline;

    public string Name => "Triangle";

    public void Initialize(VulkanWindow window)
    {
        var basePath = AppContext.BaseDirectory;
        var vertPath = Path.Combine(basePath, "Shaders", "triangle.vert");
        var fragPath = Path.Combine(basePath, "Shaders", "triangle.frag");

        var vertSource = File.ReadAllText(vertPath);
        var fragSource = File.ReadAllText(fragPath);

        _vertShaderModule = VulkanShader.CreateVertexShader(vertSource, "triangle.vert");
        _fragShaderModule = VulkanShader.CreateFragmentShader(fragSource, "triangle.frag");

        CreatePipeline(window.ImageFormat);
    }

    public void Record(CommandBuffer commandBuffer, ImageView targetImageView, Extent2D extent, Format imageFormat)
    {
        ClearValue clearColor = new()
        {
            Color = new ClearColorValue { Float32_0 = 0, Float32_1 = 0, Float32_2 = 0, Float32_3 = 1 },
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

        VulkanContext.Vk.CmdDraw(commandBuffer, 3, 1, 0, 0);
        VulkanContext.Vk.CmdEndRendering(commandBuffer);
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

            PipelineVertexInputStateCreateInfo vertexInputInfo = new()
            {
                SType = StructureType.PipelineVertexInputStateCreateInfo,
                VertexBindingDescriptionCount = 0,
                VertexAttributeDescriptionCount = 0,
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
}
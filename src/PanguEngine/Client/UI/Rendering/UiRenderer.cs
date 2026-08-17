using System.Runtime.InteropServices;
using PanguEngine.Graphics;
using GraphicsBuffer = PanguEngine.Graphics.Buffer;

namespace PanguEngine.Client.UI.Rendering;

internal sealed class UiRenderer
{
    private readonly GraphicsDevice _device;
    private readonly bool _convertSrgbToLinear;
    private readonly UiDrawBuilder _builder = new();
    private readonly FrameResources[] _frameResources;
    private readonly UiResourceManager _resourceManager;
    private readonly GraphicsPipeline _pipeline;
    private readonly DescriptorSetLayout _imageDescriptorLayout;
    private readonly GraphicsPipeline _imagePipeline;
    private bool _destroyed;

    internal UiRenderer(
        GraphicsDevice device,
        TextureFormat colorFormat,
        TextureFormat depthStencilFormat,
        uint frameSlotCount)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentOutOfRangeException.ThrowIfZero(frameSlotCount);

        _device = device;
        _convertSrgbToLinear = colorFormat is TextureFormat.R8G8B8A8Srgb or TextureFormat.B8G8R8A8Srgb;
        _frameResources = new FrameResources[checked((int)frameSlotCount)];
        for (var index = 0; index < _frameResources.Length; index++)
            _frameResources[index] = new FrameResources();

        _imageDescriptorLayout = device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription(
            [new DescriptorSetLayoutBinding(
                0,
                DescriptorType.CombinedImageSampler,
                ShaderStageFlags.Fragment)]));
        _resourceManager = new UiResourceManager(
            device,
            _imageDescriptorLayout,
            Log.CreateLogger("UI"));

        GraphicsPipeline solidPipeline;
        try
        {
            solidPipeline = CreatePipeline(
                device,
                colorFormat,
                depthStencilFormat,
                "pangu/shaders/ui_solid.vert",
                "pangu/shaders/ui_solid.frag",
                "ui_solid.vert",
                "ui_solid.frag",
                UiVertex.SolidVertexInput,
                []);
        }
        catch
        {
            _resourceManager.Destroy();
            _imageDescriptorLayout.Destroy();
            throw;
        }

        GraphicsPipeline imagePipeline;
        try
        {
            imagePipeline = CreatePipeline(
                device,
                colorFormat,
                depthStencilFormat,
                "pangu/shaders/ui_image.vert",
                "pangu/shaders/ui_image.frag",
                "ui_image.vert",
                "ui_image.frag",
                UiVertex.ImageVertexInput,
                [_imageDescriptorLayout]);
        }
        catch
        {
            solidPipeline.Destroy();
            _resourceManager.Destroy();
            _imageDescriptorLayout.Destroy();
            throw;
        }

        _pipeline = solidPipeline;
        _imagePipeline = imagePipeline;
    }

    internal void Draw(Frame frame, UiDrawCommandList commands)
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(commands);
        if (frame.FrameSlot >= (uint)_frameResources.Length)
            throw new ArgumentOutOfRangeException(nameof(frame), "Frame slot exceeds the renderer frame resource count.");

        _resourceManager.DrainFinalizedResources();
        _builder.Build(
            commands,
            frame.Width,
            frame.Height,
            _convertSrgbToLinear,
            _resourceManager.ResolveImageBinding);
        if (_builder.RectangleCount == 0)
            return;

        var resources = _frameResources[checked((int)frame.FrameSlot)];
        EnsureCapacity(resources, _builder.RectangleCount);
        var vertexBuffer = resources.VertexBuffer!;
        var indexBuffer = resources.IndexBuffer!;
        vertexBuffer.Write(_builder.Vertices);
        indexBuffer.Write(_builder.Indices);

        var commandList = frame.CommandList;
        commandList.SetViewport(0, 0, frame.Width, frame.Height);
        commandList.SetVertexBuffer(0, vertexBuffer);
        commandList.SetIndexBuffer(indexBuffer, IndexFormat.UInt32);
        var projection = new UiProjection(2f / frame.Width, 2f / frame.Height);
        foreach (var batch in _builder.Batches)
        {
            if (batch.Material.Kind == UiMaterialKind.Image)
            {
                commandList.SetGraphicsPipeline(_imagePipeline);
                commandList.SetDescriptorSet(0, batch.Material.DescriptorSet!);
            }
            else
            {
                commandList.SetGraphicsPipeline(_pipeline);
            }

            commandList.SetPushConstants(ShaderStageFlags.Vertex, 0, projection);
            commandList.SetScissor(
                batch.Scissor.X,
                batch.Scissor.Y,
                batch.Scissor.Width,
                batch.Scissor.Height);
            commandList.DrawIndexed(batch.IndexCount, firstIndex: batch.FirstIndex);
        }
    }

    internal void Destroy()
    {
        if (_destroyed)
            return;

        _resourceManager.Destroy();
        _imagePipeline.Destroy();
        _imageDescriptorLayout.Destroy();
        foreach (var frame in _frameResources)
        {
            frame.IndexBuffer?.Destroy();
            frame.VertexBuffer?.Destroy();
        }
        _pipeline.Destroy();
        _destroyed = true;
    }

    private static GraphicsPipeline CreatePipeline(
        GraphicsDevice device,
        TextureFormat colorFormat,
        TextureFormat depthStencilFormat,
        string vertexPath,
        string fragmentPath,
        string vertexName,
        string fragmentName,
        VertexInputDescription vertexInput,
        DescriptorSetLayout[] descriptorSetLayouts)
    {
        var vertexSource = Engine.ResourceManager.ReadAllText(vertexPath);
        var fragmentSource = Engine.ResourceManager.ReadAllText(fragmentPath);
        var vertexBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Vertex, vertexSource, name: vertexName);
        var fragmentBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Fragment, fragmentSource, name: fragmentName);

        GraphicsPipeline? pipeline = null;
        Shader? vertexShader = null;
        Shader? fragmentShader = null;
        try
        {
            vertexShader = device.CreateShader(new ShaderDescription(ShaderStage.Vertex, vertexBytecode, vertexName));
            fragmentShader = device.CreateShader(new ShaderDescription(ShaderStage.Fragment, fragmentBytecode, fragmentName));
            pipeline = device.CreateGraphicsPipeline(new GraphicsPipelineDescription
            {
                Shaders = [vertexShader, fragmentShader],
                VertexInput = vertexInput,
                ColorAttachmentFormats = [colorFormat],
                DescriptorSetLayouts = descriptorSetLayouts,
                PushConstantRanges = [new PushConstantRangeDescription(ShaderStageFlags.Vertex, 0, UiProjection.SizeInBytes)],
                Rasterizer = new RasterizerDescription { CullMode = CullMode.None },
                ColorBlend = new ColorBlendDescription { AlphaBlend = true },
                DepthStencil = new DepthStencilDescription(false, false, CompareOperation.Always, false, default, default),
                DepthStencilAttachmentFormat = depthStencilFormat
            });
            return pipeline;
        }
        catch
        {
            pipeline?.Destroy();
            throw;
        }
        finally
        {
            fragmentShader?.Destroy();
            vertexShader?.Destroy();
        }
    }

    private void EnsureCapacity(FrameResources frame, int requiredCapacity)
    {
        if (frame.Capacity >= requiredCapacity)
            return;

        var newCapacity = UiDrawBuilder.GrowCapacity(frame.Capacity, requiredCapacity);
        var vertexBuffer = _device.CreateBuffer(new BufferDescription(
            checked((ulong)newCapacity * 4 * UiVertex.SizeInBytes),
            BufferUsage.Vertex,
            MemoryUsage.CpuToGpu));
        GraphicsBuffer indexBuffer;
        try
        {
            indexBuffer = _device.CreateBuffer(new BufferDescription(
                checked((ulong)newCapacity * 6 * sizeof(uint)),
                BufferUsage.Index,
                MemoryUsage.CpuToGpu));
        }
        catch
        {
            vertexBuffer.Destroy();
            throw;
        }

        var oldVertexBuffer = frame.VertexBuffer;
        var oldIndexBuffer = frame.IndexBuffer;
        frame.VertexBuffer = vertexBuffer;
        frame.IndexBuffer = indexBuffer;
        frame.Capacity = newCapacity;
        oldIndexBuffer?.Destroy();
        oldVertexBuffer?.Destroy();
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct UiProjection(float clipScaleX, float clipScaleY)
    {
        internal const uint SizeInBytes = 8;
        private readonly float _clipScaleX = clipScaleX;
        private readonly float _clipScaleY = clipScaleY;
    }

    private sealed class FrameResources
    {
        internal GraphicsBuffer? VertexBuffer { get; set; }
        internal GraphicsBuffer? IndexBuffer { get; set; }
        internal int Capacity { get; set; }
    }
}

using System.Runtime.InteropServices;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Graphics;
using PanguEngine.Graphics.Text;
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
    private readonly DescriptorSetLayout _descriptorSetLayout;
    private bool _destroyed;

    internal UiRenderer(
        GraphicsDevice device,
        FontManager fontManager,
        TextureFormat colorFormat,
        TextureFormat depthStencilFormat,
        uint frameSlotCount)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(fontManager);
        ArgumentOutOfRangeException.ThrowIfZero(frameSlotCount);
        fontManager.VerifyServiceAccess();

        _device = device;
        _convertSrgbToLinear = colorFormat is TextureFormat.R8G8B8A8Srgb or TextureFormat.B8G8R8A8Srgb;
        _frameResources = new FrameResources[checked((int)frameSlotCount)];
        for (var index = 0; index < _frameResources.Length; index++)
            _frameResources[index] = new FrameResources();

        _descriptorSetLayout = device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription(
        [
            new DescriptorSetLayoutBinding(
                0,
                DescriptorType.SampledImage,
                ShaderStageFlags.Fragment,
                UiTextureTable.SlotCount),
            new DescriptorSetLayoutBinding(1, DescriptorType.Sampler, ShaderStageFlags.Fragment),
            new DescriptorSetLayoutBinding(2, DescriptorType.Sampler, ShaderStageFlags.Fragment)
        ]));
        _resourceManager = new UiResourceManager(
            device,
            fontManager,
            _descriptorSetLayout,
            frameSlotCount,
            Log.CreateLogger("UI"));
        _pipeline = CreatePipeline(
            device,
            colorFormat,
            depthStencilFormat,
            "pangu/shaders/ui.vert",
            "pangu/shaders/ui.frag",
            "ui.vert",
            "ui.frag",
            _descriptorSetLayout);
    }

    internal void PrepareFrame(Frame frame)
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
        ArgumentNullException.ThrowIfNull(frame);
        _resourceManager.PrepareFrame(frame.FrameSlot);
    }

    internal void Draw(Frame frame, UiDrawCommandList commands)
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(commands);
        if (frame.FrameSlot >= (uint)_frameResources.Length)
            throw new ArgumentOutOfRangeException(nameof(frame),
                "Frame slot exceeds the renderer frame resource count.");

        _builder.Build(
            commands,
            frame.Width,
            frame.Height,
            _convertSrgbToLinear,
            _resourceManager.ResolveImageBinding,
            _resourceManager.ResolveGlyphBinding);
        _resourceManager.SynchronizeAfterBuild(frame.FrameSlot);
        if (_builder.IndexCount == 0)
            return;

        var resources = _frameResources[checked((int)frame.FrameSlot)];
        EnsureCapacity(resources, _builder.VertexCount, _builder.IndexCount);
        var vertexBuffer = resources.VertexBuffer!;
        var indexBuffer = resources.IndexBuffer!;
        vertexBuffer.Write(_builder.Vertices);
        indexBuffer.Write(_builder.Indices);

        var commandList = frame.CommandList;
        commandList.SetGraphicsPipeline(_pipeline);
        commandList.SetDescriptorSet(0, _resourceManager.GetTextureDescriptorSet(frame.FrameSlot));
        commandList.SetViewport(0, 0, frame.Width, frame.Height);
        commandList.SetVertexBuffer(0, vertexBuffer);
        commandList.SetIndexBuffer(indexBuffer, IndexFormat.UInt32);
        var projection = new UiProjection(2f / frame.Width, 2f / frame.Height);
        commandList.SetPushConstants(ShaderStageFlags.Vertex, 0, projection);
        foreach (var batch in _builder.Batches)
        {
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
        _destroyed = true;

        _resourceManager.Destroy();
        foreach (var frame in _frameResources)
        {
            frame.IndexBuffer?.Destroy();
            frame.VertexBuffer?.Destroy();
        }

        _pipeline.Destroy();
        _descriptorSetLayout.Destroy();
    }

    private static GraphicsPipeline CreatePipeline(
        GraphicsDevice device,
        TextureFormat colorFormat,
        TextureFormat depthStencilFormat,
        string vertexPath,
        string fragmentPath,
        string vertexName,
        string fragmentName,
        DescriptorSetLayout descriptorSetLayout)
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
            fragmentShader =
                device.CreateShader(new ShaderDescription(ShaderStage.Fragment, fragmentBytecode, fragmentName));
            pipeline = device.CreateGraphicsPipeline(new GraphicsPipelineDescription
            {
                Shaders = [vertexShader, fragmentShader],
                VertexInput = UiVertex.VertexInput,
                ColorAttachmentFormats = [colorFormat],
                DescriptorSetLayouts = [descriptorSetLayout],
                PushConstantRanges =
                    [new PushConstantRangeDescription(ShaderStageFlags.Vertex, 0, UiProjection.SizeInBytes)],
                Rasterizer = new RasterizerDescription { CullMode = CullMode.None },
                ColorBlend = new ColorBlendDescription { AlphaBlend = true },
                DepthStencil =
                    new DepthStencilDescription(false, false, CompareOperation.Always, false, default, default),
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

    private void EnsureCapacity(FrameResources frame, int requiredVertexCount, int requiredIndexCount)
    {
        if (frame.VertexCapacity < requiredVertexCount)
        {
            var capacity = UiDrawBuilder.GrowCapacity(frame.VertexCapacity, requiredVertexCount);
            var vertexBuffer = _device.CreateBuffer(new BufferDescription(
                checked((ulong)capacity * UiVertex.SizeInBytes),
                BufferUsage.Vertex,
                MemoryUsage.CpuToGpu));
            var previous = frame.VertexBuffer;
            frame.VertexBuffer = vertexBuffer;
            frame.VertexCapacity = capacity;
            previous?.Destroy();
        }

        if (frame.IndexCapacity < requiredIndexCount)
        {
            var capacity = UiDrawBuilder.GrowCapacity(frame.IndexCapacity, requiredIndexCount);
            var indexBuffer = _device.CreateBuffer(new BufferDescription(
                checked((ulong)capacity * sizeof(uint)),
                BufferUsage.Index,
                MemoryUsage.CpuToGpu));
            var previous = frame.IndexBuffer;
            frame.IndexBuffer = indexBuffer;
            frame.IndexCapacity = capacity;
            previous?.Destroy();
        }
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
        internal int VertexCapacity { get; set; }
        internal int IndexCapacity { get; set; }
    }
}
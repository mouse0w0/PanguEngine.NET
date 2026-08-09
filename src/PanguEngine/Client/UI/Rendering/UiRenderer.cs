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
    private readonly Shader _vertexShader;
    private readonly Shader _fragmentShader;
    private readonly GraphicsPipeline _pipeline;
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

        var resources = CreatePipelineResources(device, colorFormat, depthStencilFormat);
        _vertexShader = resources.VertexShader;
        _fragmentShader = resources.FragmentShader;
        _pipeline = resources.Pipeline;
    }

    internal void Draw(Frame frame, UiDrawCommandList commands, double uiScale)
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(commands);
        if (!double.IsFinite(uiScale) || uiScale <= 0)
            throw new ArgumentOutOfRangeException(nameof(uiScale), "UI scale must be finite and greater than zero.");
        if (frame.FrameSlot >= (uint)_frameResources.Length)
            throw new ArgumentOutOfRangeException(nameof(frame), "Frame slot exceeds the renderer frame resource count.");

        _builder.Build(commands, frame.Width, frame.Height, uiScale, _convertSrgbToLinear);
        if (_builder.RectangleCount == 0)
            return;

        var resources = _frameResources[checked((int)frame.FrameSlot)];
        EnsureCapacity(resources, _builder.RectangleCount);
        var vertexBuffer = resources.VertexBuffer!;
        var indexBuffer = resources.IndexBuffer!;
        vertexBuffer.Write(_builder.Vertices);
        indexBuffer.Write(_builder.Indices);

        var commandList = frame.CommandList;
        commandList.SetGraphicsPipeline(_pipeline);
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

        foreach (var frame in _frameResources)
        {
            frame.IndexBuffer?.Destroy();
            frame.VertexBuffer?.Destroy();
        }
        _pipeline.Destroy();
        _fragmentShader.Destroy();
        _vertexShader.Destroy();
        _destroyed = true;
    }

    private static PipelineResources CreatePipelineResources(
        GraphicsDevice device,
        TextureFormat colorFormat,
        TextureFormat depthStencilFormat)
    {
        var vertexSource = Engine.ResourceManager.ReadAllText("pangu/shaders/ui_solid.vert");
        var fragmentSource = Engine.ResourceManager.ReadAllText("pangu/shaders/ui_solid.frag");
        var vertexBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Vertex, vertexSource, name: "ui_solid.vert");
        var fragmentBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Fragment, fragmentSource, name: "ui_solid.frag");

        Shader? vertexShader = null;
        Shader? fragmentShader = null;
        GraphicsPipeline? pipeline = null;
        try
        {
            vertexShader = device.CreateShader(new ShaderDescription(ShaderStage.Vertex, vertexBytecode, "ui_solid.vert"));
            fragmentShader = device.CreateShader(new ShaderDescription(ShaderStage.Fragment, fragmentBytecode, "ui_solid.frag"));
            pipeline = device.CreateGraphicsPipeline(new GraphicsPipelineDescription
            {
                Shaders = [vertexShader, fragmentShader],
                VertexInput = UiVertex.VertexInput,
                ColorAttachmentFormats = [colorFormat],
                DescriptorSetLayouts = [],
                PushConstantRanges = [new PushConstantRangeDescription(ShaderStageFlags.Vertex, 0, UiProjection.SizeInBytes)],
                Rasterizer = new RasterizerDescription { CullMode = CullMode.None },
                ColorBlend = new ColorBlendDescription { AlphaBlend = true },
                DepthStencil = new DepthStencilDescription(false, false, CompareOperation.Always, false, default, default),
                DepthStencilAttachmentFormat = depthStencilFormat
            });
            return new PipelineResources(vertexShader, fragmentShader, pipeline);
        }
        catch
        {
            pipeline?.Destroy();
            fragmentShader?.Destroy();
            vertexShader?.Destroy();
            throw;
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

    private sealed record PipelineResources(
        Shader VertexShader,
        Shader FragmentShader,
        GraphicsPipeline Pipeline);

    private sealed class FrameResources
    {
        internal GraphicsBuffer? VertexBuffer { get; set; }
        internal GraphicsBuffer? IndexBuffer { get; set; }
        internal int Capacity { get; set; }
    }
}

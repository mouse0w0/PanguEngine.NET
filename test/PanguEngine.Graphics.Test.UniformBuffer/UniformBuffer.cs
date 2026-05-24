using System.Diagnostics;
using System.Runtime.InteropServices;
using GraphicsBuffer = PanguEngine.Graphics.Buffer;

namespace PanguEngine.Graphics.Test.UniformBuffer;

/// <summary>
/// Entry point for the uniform buffer graphics test.
/// </summary>
internal static class UniformBuffer
{
    private static void Main()
    {
        new GraphicsTestApp(new UniformBufferScene()).Run();
    }
}

/// <summary>
/// Renders a triangle with per-frame uniform buffer bindings.
/// </summary>
internal sealed class UniformBufferScene : IGraphicsTestScene
{
    private readonly Vertex[] _vertices =
    [
        new(0.0f, -0.45f, 1, 1, 1),
        new(0.45f, 0.45f, 1, 1, 1),
        new(-0.45f, 0.45f, 1, 1, 1),
    ];

    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private Shader _vertShader = null!;
    private Shader _fragShader = null!;
    private DescriptorSetLayout _descriptorSetLayout = null!;
    private DescriptorSet[] _descriptorSets = null!;
    private GraphicsPipeline _pipeline = null!;
    private GraphicsBuffer _vertexBuffer = null!;
    private GraphicsBuffer _uniformBuffer = null!;
    private UploadHandle _uploadHandle = null!;
    private Presenter _presenter = null!;
    private ulong _uniformStride;

    /// <inheritdoc/>
    public string Name => "UniformBuffer";

    /// <inheritdoc/>
    public void Initialize(Presenter presenter)
    {
        _presenter = presenter;
        CreateVertexBuffer();
        CreateUniformBuffer(presenter.MaxFramesInFlight);
        CreateDescriptorSetLayout();
        CreateDescriptorSets(presenter.MaxFramesInFlight);
        CreateShaders();
        CreatePipeline(presenter.ColorFormat);
    }

    /// <inheritdoc/>
    public void Record(Frame frame, CommandList commands)
    {
        if (!_uploadHandle.IsCompleted)
            throw new InvalidOperationException(
                "Vertex buffer upload did not complete after flushing pending uploads.");

        var frameIndex = _presenter.CurrentFrameIndex;
        var descriptorIndex = checked((int)frameIndex);
        WriteFrameUniform(frameIndex);

        commands.BeginRendering(new RenderingDescription(new ClearColor(0.01f, 0.012f, 0.018f, 1)));
        commands.SetGraphicsPipeline(_pipeline);
        commands.SetViewport(0, 0, frame.Width, frame.Height);
        commands.SetScissor(0, 0, frame.Width, frame.Height);
        commands.SetVertexBuffer(0, _vertexBuffer);
        commands.SetDescriptorSet(0, _descriptorSets[descriptorIndex]);
        commands.Draw((uint)_vertices.Length);
        commands.EndRendering();
    }

    /// <inheritdoc/>
    public void Destroy()
    {
        _pipeline.Destroy();
        foreach (var descriptorSet in _descriptorSets)
            descriptorSet.Destroy();

        _descriptorSetLayout.Destroy();
        _fragShader.Destroy();
        _vertShader.Destroy();
        _uniformBuffer.Destroy();
        _vertexBuffer.Destroy();
    }

    private void CreateVertexBuffer()
    {
        var size = (ulong)(Marshal.SizeOf<Vertex>() * _vertices.Length);
        _vertexBuffer = GraphicsContext.Device.CreateBuffer(new BufferDescription(
            size,
            BufferUsage.TransferDestination | BufferUsage.Vertex,
            MemoryUsage.GpuOnly));
        _uploadHandle = GraphicsContext.Device.UploadBuffer(_vertexBuffer, _vertices);
    }

    private void CreateUniformBuffer(uint frameCount)
    {
        var uniformSize = (ulong)Marshal.SizeOf<FrameUniform>();
        _uniformStride = GraphicsContext.Device.GetAlignedUniformSize(uniformSize);
        _uniformBuffer = GraphicsContext.Device.CreateBuffer(new BufferDescription(
            _uniformStride * frameCount,
            BufferUsage.Uniform,
            MemoryUsage.CpuToGpu));
    }

    private void CreateDescriptorSetLayout()
    {
        _descriptorSetLayout = GraphicsContext.Device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription(
            new[]
            {
                new DescriptorSetLayoutBinding(0, DescriptorType.UniformBuffer, ShaderStage.Vertex)
            }));
    }

    private void CreateDescriptorSets(uint frameCount)
    {
        var descriptorSets = new DescriptorSet[checked((int)frameCount)];
        var uniformSize = (ulong)Marshal.SizeOf<FrameUniform>();

        for (var i = 0; i < descriptorSets.Length; i++)
        {
            descriptorSets[i] = GraphicsContext.Device.CreateDescriptorSet(new DescriptorSetDescription(
                _descriptorSetLayout,
                new[]
                {
                    new DescriptorSetBinding(0, _uniformBuffer, _uniformStride * (uint)i, uniformSize)
                }));
        }

        _descriptorSets = descriptorSets;
    }

    private void CreateShaders()
    {
        var basePath = AppContext.BaseDirectory;
        var vertPath = Path.Combine(basePath, "Shaders", "uniform_buffer.vert");
        var fragPath = Path.Combine(basePath, "Shaders", "uniform_buffer.frag");

        var vertSource = File.ReadAllText(vertPath);
        var fragSource = File.ReadAllText(fragPath);

        var vertBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Vertex, vertSource, name: "uniform_buffer.vert");
        var fragBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Fragment, fragSource, name: "uniform_buffer.frag");

        _vertShader = GraphicsContext.Device.CreateShader(new ShaderDescription(
            ShaderStage.Vertex,
            vertBytecode,
            Name: "uniform_buffer.vert"));
        _fragShader = GraphicsContext.Device.CreateShader(new ShaderDescription(
            ShaderStage.Fragment,
            fragBytecode,
            Name: "uniform_buffer.frag"));
    }

    private void CreatePipeline(TextureFormat colorFormat)
    {
        _pipeline = GraphicsContext.Device.CreateGraphicsPipeline(new GraphicsPipelineDescription(
            new[] { _vertShader, _fragShader },
            CreateVertexInputDescription(),
            ColorAttachmentFormat: colorFormat,
            DescriptorSetLayouts: new[] { _descriptorSetLayout }));
    }

    private static VertexInputDescription CreateVertexInputDescription()
    {
        return new VertexInputDescription(
            new[]
            {
                new VertexBufferLayoutDescription(0, (uint)Marshal.SizeOf<Vertex>())
            },
            new[]
            {
                new VertexAttributeDescription(0, 0, VertexAttributeFormat.Float32x2, 0),
                new VertexAttributeDescription(1, 0, VertexAttributeFormat.Float32x3, 8)
            });
    }

    private void WriteFrameUniform(uint frameIndex)
    {
        var time = (float)_stopwatch.Elapsed.TotalSeconds;
        var uniform = new FrameUniform
        {
            TintR = 0.5f + MathF.Sin(time) * 0.5f,
            TintG = 0.5f + MathF.Sin(time + 2.0943952f) * 0.5f,
            TintB = 0.5f + MathF.Sin(time + 4.1887903f) * 0.5f,
            TintA = 1,
            OffsetX = MathF.Sin(time) * 0.25f,
            OffsetY = 0,
        };

        _uniformBuffer.Write(in uniform, _uniformStride * frameIndex);
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
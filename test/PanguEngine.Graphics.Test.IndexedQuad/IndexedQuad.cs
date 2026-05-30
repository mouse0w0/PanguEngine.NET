using System.Runtime.InteropServices;
using PanguEngine.Windowing;
using GraphicsBuffer = PanguEngine.Graphics.Buffer;

namespace PanguEngine.Graphics.Test.IndexedQuad;

/// <summary>
/// Entry point for the indexed quad graphics test.
/// </summary>
internal static class IndexedQuad
{
    private static void Main()
    {
        new GraphicsTestApp(new IndexedQuadScene()).Run();
    }
}

/// <summary>
/// Renders a quad with an index buffer.
/// </summary>
internal sealed class IndexedQuadScene : IGraphicsTestScene
{
    private readonly Vertex[] _vertices =
    [
        new(-0.5f, -0.5f, 1, 0, 0),
        new(0.5f, -0.5f, 0, 1, 0),
        new(0.5f, 0.5f, 0, 0, 1),
        new(-0.5f, 0.5f, 1, 1, 0),
    ];

    private readonly ushort[] _indices = [0, 1, 2, 2, 3, 0];

    private Shader _vertShader = null!;
    private Shader _fragShader = null!;
    private GraphicsPipeline _pipeline = null!;
    private GraphicsBuffer _vertexBuffer = null!;
    private GraphicsBuffer _indexBuffer = null!;
    private UploadHandle _vertexUploadHandle = null!;
    private UploadHandle _indexUploadHandle = null!;

    /// <inheritdoc/>
    public string Name => "IndexedQuad";

    /// <inheritdoc/>
    public void Initialize(Window window)
    {
        CreateBuffers();
        CreateShaders();
        CreatePipeline(window.Presenter.ColorFormat);
    }

    /// <inheritdoc/>
    public void Record(Frame frame, CommandList commands)
    {
        if (!_vertexUploadHandle.IsCompleted)
            throw new InvalidOperationException(
                "Vertex buffer upload did not complete after flushing pending uploads.");
        if (!_indexUploadHandle.IsCompleted)
            throw new InvalidOperationException(
                "Index buffer upload did not complete after flushing pending uploads.");

        commands.BeginRendering(new RenderingDescription(new ClearColor(0.01f, 0.01f, 0.015f, 1)));
        commands.SetGraphicsPipeline(_pipeline);
        commands.SetViewport(0, 0, frame.Width, frame.Height);
        commands.SetScissor(0, 0, frame.Width, frame.Height);
        commands.SetVertexBuffer(0, _vertexBuffer);
        commands.SetIndexBuffer(_indexBuffer, IndexFormat.UInt16);
        commands.DrawIndexed((uint)_indices.Length);
        commands.EndRendering();
    }

    /// <inheritdoc/>
    public void Destroy()
    {
        _indexBuffer.Destroy();
        _vertexBuffer.Destroy();
        _pipeline.Destroy();
        _fragShader.Destroy();
        _vertShader.Destroy();
    }

    private void CreateBuffers()
    {
        var vertexBufferSize = (ulong)(Marshal.SizeOf<Vertex>() * _vertices.Length);
        _vertexBuffer = GraphicsContext.Device.CreateBuffer(new BufferDescription(
            vertexBufferSize,
            BufferUsage.TransferDestination | BufferUsage.Vertex,
            MemoryUsage.GpuOnly));
        _vertexUploadHandle = GraphicsContext.Device.UploadBuffer(_vertexBuffer, _vertices);

        var indexBufferSize = (ulong)(sizeof(ushort) * _indices.Length);
        _indexBuffer = GraphicsContext.Device.CreateBuffer(new BufferDescription(
            indexBufferSize,
            BufferUsage.TransferDestination | BufferUsage.Index,
            MemoryUsage.GpuOnly));
        _indexUploadHandle = GraphicsContext.Device.UploadBuffer(_indexBuffer, _indices);
    }

    private void CreateShaders()
    {
        var basePath = AppContext.BaseDirectory;
        var vertPath = Path.Combine(basePath, "Shaders", "indexed_quad.vert");
        var fragPath = Path.Combine(basePath, "Shaders", "indexed_quad.frag");

        var vertSource = File.ReadAllText(vertPath);
        var fragSource = File.ReadAllText(fragPath);

        var vertBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Vertex, vertSource, name: "indexed_quad.vert");
        var fragBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Fragment, fragSource, name: "indexed_quad.frag");

        _vertShader = GraphicsContext.Device.CreateShader(new ShaderDescription(
            ShaderStage.Vertex,
            vertBytecode,
            Name: "indexed_quad.vert"));
        _fragShader = GraphicsContext.Device.CreateShader(new ShaderDescription(
            ShaderStage.Fragment,
            fragBytecode,
            Name: "indexed_quad.frag"));
    }

    private void CreatePipeline(TextureFormat colorFormat)
    {
        _pipeline = GraphicsContext.Device.CreateGraphicsPipeline(new GraphicsPipelineDescription(
            new[] { _vertShader, _fragShader },
            CreateVertexInputDescription(),
            ColorAttachmentFormat: colorFormat));
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

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct Vertex(float X, float Y, float R, float G, float B);
}
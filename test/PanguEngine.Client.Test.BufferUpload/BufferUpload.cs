using System.Runtime.InteropServices;
using PanguEngine.Graphics;
using PanguEngine.Windowing;
using GraphicsBuffer = PanguEngine.Graphics.Buffer;

namespace PanguEngine.Client.Test.BufferUpload;

/// <summary>
/// Entry point for the buffer upload graphics test.
/// </summary>
internal static class BufferUpload
{
    private static void Main()
    {
        new GraphicsTestApp(new BufferUploadScene()).Run();
    }
}

/// <summary>
/// Renders a triangle from an uploaded vertex buffer.
/// </summary>
internal sealed class BufferUploadScene : IGraphicsTestScene
{
    private readonly Vertex[] _vertices =
    [
        new(0.0f, -0.5f, 1, 0, 0),
        new(0.5f, 0.5f, 0, 1, 0),
        new(-0.5f, 0.5f, 0, 0, 1),
    ];

    private Shader _vertShader = null!;
    private Shader _fragShader = null!;
    private GraphicsPipeline _pipeline = null!;
    private GraphicsBuffer _vertexBuffer = null!;
    private UploadHandle _uploadHandle = null!;

    /// <inheritdoc/>
    public string Name => "BufferUpload";

    /// <inheritdoc/>
    public void Initialize(Window window)
    {
        CreateVertexBuffer();
        CreateShaders();
        CreatePipeline(window.Presenter.ColorFormat);
    }

    /// <inheritdoc/>
    public void Record(Frame frame, CommandList commands)
    {
        if (!_uploadHandle.IsCompleted)
            throw new InvalidOperationException(
                "Vertex buffer upload did not complete after flushing pending uploads.");

        commands.BeginRendering(new RenderingDescription(new ClearColor(0.01f, 0.01f, 0.015f, 1)));
        commands.SetGraphicsPipeline(_pipeline);
        commands.SetViewport(0, 0, frame.Width, frame.Height);
        commands.SetScissor(0, 0, frame.Width, frame.Height);
        commands.SetVertexBuffer(0, _vertexBuffer);
        commands.Draw((uint)_vertices.Length);
        commands.EndRendering();
    }

    /// <inheritdoc/>
    public void Destroy()
    {
        _vertexBuffer.Destroy();
        _pipeline.Destroy();
        _fragShader.Destroy();
        _vertShader.Destroy();
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

    private void CreateShaders()
    {
        var basePath = AppContext.BaseDirectory;
        var vertPath = Path.Combine(basePath, "Shaders", "buffer_upload.vert");
        var fragPath = Path.Combine(basePath, "Shaders", "buffer_upload.frag");

        var vertSource = File.ReadAllText(vertPath);
        var fragSource = File.ReadAllText(fragPath);

        var vertBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Vertex, vertSource, name: "buffer_upload.vert");
        var fragBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Fragment, fragSource, name: "buffer_upload.frag");

        _vertShader = GraphicsContext.Device.CreateShader(new ShaderDescription(
            ShaderStage.Vertex,
            vertBytecode,
            Name: "buffer_upload.vert"));
        _fragShader = GraphicsContext.Device.CreateShader(new ShaderDescription(
            ShaderStage.Fragment,
            fragBytecode,
            Name: "buffer_upload.frag"));
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
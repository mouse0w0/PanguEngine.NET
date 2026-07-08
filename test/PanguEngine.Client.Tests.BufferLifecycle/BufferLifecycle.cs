using System.Diagnostics;
using System.Runtime.InteropServices;
using PanguEngine.Graphics;
using PanguEngine.Windowing;
using GraphicsBuffer = PanguEngine.Graphics.Buffer;

namespace PanguEngine.Client.Tests.BufferLifecycle;

/// <summary>
/// Entry point for the buffer lifecycle graphics test.
/// </summary>
internal static class BufferLifecycle
{
    private static void Main()
    {
        ClientTestApp.Run(new BufferLifecycleScene());
    }
}

/// <summary>
/// Renders a triangle from a per-frame vertex buffer and destroys it after recording.
/// </summary>
internal sealed class BufferLifecycleScene : IClientTestScene
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    private Shader _vertShader = null!;
    private Shader _fragShader = null!;
    private GraphicsPipeline _pipeline = null!;
    private Mesh _mesh = null!;
    private Presenter _presenter = null!;

    /// <inheritdoc/>
    public string Name => "BufferLifecycle";

    /// <inheritdoc/>
    public void Initialize(Window window)
    {
        _presenter = window.Presenter;
        CreateShaders();
        CreatePipeline(_presenter.ColorFormat);
        window.Render += (_, _) => DrawFrame();
    }

    /// <inheritdoc/>
    public void Destroy()
    {
        _pipeline.Destroy();
        _fragShader.Destroy();
        _vertShader.Destroy();
    }

    private void DrawFrame()
    {
        _mesh = CreateMesh();

        if (!_presenter.TryBeginFrame(out var frame))
            return;

        try
        {
            var commands = frame.CommandList;
            commands.Begin();
            commands.BeginRendering(new RenderingDescription(
                frame.Width,
                frame.Height,
                [
                    new ColorAttachmentDescription(frame.ColorOutput, new ClearColor(0.008f, 0.01f, 0.016f, 1)),
                ]));
            commands.SetGraphicsPipeline(_pipeline);
            commands.SetViewport(0, 0, frame.Width, frame.Height);
            commands.SetScissor(0, 0, frame.Width, frame.Height);

            if (!_mesh.UploadHandle.CheckSuccess())
                throw new InvalidOperationException(
                    "Mesh buffer upload did not complete after flushing pending uploads.");

            commands.SetVertexBuffer(0, _mesh.Buffer);
            commands.Draw(_mesh.VertexCount);
            commands.EndRendering();
            commands.PrepareForPresent(frame.ColorOutput);
            commands.End();

            _mesh.Buffer.Destroy();
        }
        finally
        {
            _presenter.EndFrame(frame);
        }
    }

    private Mesh CreateMesh()
    {
        var vertices = CreateVertices();
        var size = (ulong)(Marshal.SizeOf<Vertex>() * vertices.Length);
        var buffer = ClientTestApp.Current.Device.CreateBuffer(new BufferDescription(
            size,
            BufferUsage.TransferDestination | BufferUsage.Vertex,
            MemoryUsage.GpuOnly));
        var uploadHandle = ClientTestApp.Current.Device.UploadBuffer(buffer, vertices);
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
        var vertPath = Path.Combine(basePath, "shaders", "buffer_lifecycle.vert");
        var fragPath = Path.Combine(basePath, "shaders", "buffer_lifecycle.frag");

        var vertSource = File.ReadAllText(vertPath);
        var fragSource = File.ReadAllText(fragPath);

        var vertBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Vertex, vertSource, name: "buffer_lifecycle.vert");
        var fragBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Fragment, fragSource, name: "buffer_lifecycle.frag");

        _vertShader = ClientTestApp.Current.Device.CreateShader(new ShaderDescription(
            ShaderStage.Vertex,
            vertBytecode,
            Name: "buffer_lifecycle.vert"));
        _fragShader = ClientTestApp.Current.Device.CreateShader(new ShaderDescription(
            ShaderStage.Fragment,
            fragBytecode,
            Name: "buffer_lifecycle.frag"));
    }

    private void CreatePipeline(TextureFormat colorFormat)
    {
        _pipeline = ClientTestApp.Current.Device.CreateGraphicsPipeline(new GraphicsPipelineDescription(
            [_vertShader, _fragShader],
            CreateVertexInputDescription(),
            ColorAttachmentFormats: [colorFormat],
            DescriptorSetLayouts: []));
    }

    private static VertexInputDescription CreateVertexInputDescription()
    {
        return new VertexInputDescription(
            [
                new VertexBufferLayoutDescription(0, (uint)Marshal.SizeOf<Vertex>())
            ],
            [
                new VertexAttributeDescription(0, 0, VertexAttributeFormat.Float32x2, 0),
                new VertexAttributeDescription(1, 0, VertexAttributeFormat.Float32x3, 8)
            ]);
    }

    private sealed record Mesh(
        GraphicsBuffer Buffer,
        UploadHandle UploadHandle,
        uint VertexCount);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct Vertex(float X, float Y, float R, float G, float B);
}
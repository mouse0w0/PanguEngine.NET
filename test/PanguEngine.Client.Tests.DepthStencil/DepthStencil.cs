using System.Runtime.InteropServices;
using PanguEngine.Graphics;
using PanguEngine.Windowing;
using GraphicsBuffer = PanguEngine.Graphics.Buffer;

namespace PanguEngine.Client.Tests.DepthStencil;

/// <summary>
/// Entry point for the depth/stencil graphics test.
/// </summary>
internal static class DepthStencil
{
    private static void Main()
    {
        ClientTestApp.Run(new DepthStencilScene());
    }
}

/// <summary>
/// Renders overlapping geometry using a depth/stencil attachment.
/// </summary>
internal sealed class DepthStencilScene : IClientTestScene
{
    private readonly Vertex[] _vertices =
    [
        new(-0.35f, -0.35f, 0.2f, 0.0f, 0.9f, 0.2f),
        new(0.35f, -0.35f, 0.2f, 0.0f, 0.9f, 0.2f),
        new(0.35f, 0.35f, 0.2f, 0.0f, 0.9f, 0.2f),
        new(-0.35f, 0.35f, 0.2f, 0.0f, 0.9f, 0.2f),
        new(-0.7f, -0.7f, 0.6f, 0.9f, 0.1f, 0.1f),
        new(0.7f, -0.7f, 0.6f, 0.9f, 0.1f, 0.1f),
        new(0.7f, 0.7f, 0.6f, 0.9f, 0.1f, 0.1f),
        new(-0.7f, 0.7f, 0.6f, 0.9f, 0.1f, 0.1f)
    ];

    private readonly ushort[] _indices =
    [
        0, 1, 2, 2, 3, 0,
        4, 5, 6, 6, 7, 4
    ];

    private Shader _vertShader = null!;
    private Shader _fragShader = null!;
    private GraphicsPipeline _pipeline = null!;
    private GraphicsBuffer _vertexBuffer = null!;
    private GraphicsBuffer _indexBuffer = null!;
    private UploadHandle _vertexUploadHandle = null!;
    private UploadHandle _indexUploadHandle = null!;
    private Presenter _presenter = null!;
    private Texture?[] _depthStencilAttachments = [];
    private uint _depthStencilWidth;
    private uint _depthStencilHeight;

    /// <inheritdoc/>
    public string Name => "DepthStencil";

    /// <inheritdoc/>
    public void Initialize(Window window)
    {
        _presenter = window.Presenter;
        _depthStencilAttachments = new Texture?[checked((int)_presenter.MaxFramesInFlight)];
        CreateBuffers();
        CreateShaders();
        CreatePipeline(_presenter.ColorFormat);
        window.Render += (_, _) => DrawFrame();
    }

    /// <inheritdoc/>
    public void Destroy()
    {
        foreach (var depthStencilAttachment in _depthStencilAttachments)
            depthStencilAttachment?.Destroy();
        _indexBuffer.Destroy();
        _vertexBuffer.Destroy();
        _pipeline.Destroy();
        _fragShader.Destroy();
        _vertShader.Destroy();
    }

    private void DrawFrame()
    {
        var depthStencilAttachment = EnsureDepthStencilAttachment();

        if (!_presenter.TryBeginFrame(out var frame))
            return;

        if (!_vertexUploadHandle.IsCompleted)
            throw new InvalidOperationException(
                "Vertex buffer upload did not complete after flushing pending uploads.");
        if (!_indexUploadHandle.IsCompleted)
            throw new InvalidOperationException(
                "Index buffer upload did not complete after flushing pending uploads.");

        try
        {
            var commands = frame.CommandList;
            commands.Begin();
            commands.BeginRendering(new RenderingDescription([
                new ColorAttachmentDescription(frame.ColorOutput, new ClearColor(0.01f, 0.01f, 0.015f, 1)),
            ], new DepthStencilAttachmentDescription(
                depthStencilAttachment,
                DepthClearValue: 1,
                StencilClearValue: 0)));
            commands.SetGraphicsPipeline(_pipeline);
            commands.SetViewport(0, 0, frame.Width, frame.Height);
            commands.SetScissor(0, 0, frame.Width, frame.Height);
            commands.SetVertexBuffer(0, _vertexBuffer);
            commands.SetIndexBuffer(_indexBuffer, IndexFormat.UInt16);
            commands.DrawIndexed((uint)_indices.Length);
            commands.EndRendering();
            commands.PrepareForPresent();
            commands.End();
        }
        finally
        {
            _presenter.EndFrame(frame);
        }
    }

    private Texture EnsureDepthStencilAttachment()
    {
        if (_depthStencilWidth != _presenter.Width || _depthStencilHeight != _presenter.Height)
        {
            ClientTestApp.Current.Device.WaitIdle();
            for (var i = 0; i < _depthStencilAttachments.Length; i++)
            {
                _depthStencilAttachments[i]?.Destroy();
                _depthStencilAttachments[i] = null;
            }

            _depthStencilWidth = _presenter.Width;
            _depthStencilHeight = _presenter.Height;
        }

        var frameIndex = checked((int)_presenter.CurrentFrameIndex);
        return _depthStencilAttachments[frameIndex] ??= ClientTestApp.Current.Device.CreateTexture(
            new TextureDescription(
                TextureDimension.Type2D,
                TextureFormat.Depth24UnormStencil8,
                _depthStencilWidth,
                _depthStencilHeight,
                1,
                1,
                1,
                TextureUsage.DepthStencilAttachment));
    }

    private void CreateBuffers()
    {
        var vertexBufferSize = (ulong)(Marshal.SizeOf<Vertex>() * _vertices.Length);
        _vertexBuffer = ClientTestApp.Current.Device.CreateBuffer(new BufferDescription(
            vertexBufferSize,
            BufferUsage.TransferDestination | BufferUsage.Vertex,
            MemoryUsage.GpuOnly));
        _vertexUploadHandle = ClientTestApp.Current.Device.UploadBuffer(_vertexBuffer, _vertices);

        var indexBufferSize = (ulong)(sizeof(ushort) * _indices.Length);
        _indexBuffer = ClientTestApp.Current.Device.CreateBuffer(new BufferDescription(
            indexBufferSize,
            BufferUsage.TransferDestination | BufferUsage.Index,
            MemoryUsage.GpuOnly));
        _indexUploadHandle = ClientTestApp.Current.Device.UploadBuffer(_indexBuffer, _indices);
    }

    private void CreateShaders()
    {
        var basePath = AppContext.BaseDirectory;
        var vertPath = Path.Combine(basePath, "shaders", "depth_stencil.vert");
        var fragPath = Path.Combine(basePath, "shaders", "depth_stencil.frag");

        var vertSource = File.ReadAllText(vertPath);
        var fragSource = File.ReadAllText(fragPath);

        var vertBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Vertex, vertSource, name: "depth_stencil.vert");
        var fragBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Fragment, fragSource, name: "depth_stencil.frag");

        _vertShader = ClientTestApp.Current.Device.CreateShader(new ShaderDescription(
            ShaderStage.Vertex,
            vertBytecode,
            Name: "depth_stencil.vert"));
        _fragShader = ClientTestApp.Current.Device.CreateShader(new ShaderDescription(
            ShaderStage.Fragment,
            fragBytecode,
            Name: "depth_stencil.frag"));
    }

    private void CreatePipeline(TextureFormat colorFormat)
    {
        _pipeline = ClientTestApp.Current.Device.CreateGraphicsPipeline(new GraphicsPipelineDescription(
            [_vertShader, _fragShader],
            CreateVertexInputDescription(),
            ColorAttachmentFormats: [colorFormat],
            DescriptorSetLayouts: [],
            DepthStencil: new DepthStencilDescription(
                DepthTestEnabled: true,
                DepthWriteEnabled: true,
                DepthCompareOperation: CompareOperation.LessOrEqual,
                StencilTestEnabled: true,
                FrontFace: new(CompareOperation.Always),
                BackFace: new(CompareOperation.Always)),
            DepthStencilAttachmentFormat: TextureFormat.Depth24UnormStencil8));
    }

    private static VertexInputDescription CreateVertexInputDescription()
    {
        return new VertexInputDescription(
            [
                new VertexBufferLayoutDescription(0, (uint)Marshal.SizeOf<Vertex>())
            ],
            [
                new VertexAttributeDescription(0, 0, VertexAttributeFormat.Float32x3, 0),
                new VertexAttributeDescription(1, 0, VertexAttributeFormat.Float32x3, 12)
            ]);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct Vertex(float X, float Y, float Z, float R, float G, float B);
}
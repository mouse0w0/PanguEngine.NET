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
    private Texture?[] _depthStencilTextures = [];
    private TextureView?[] _depthStencilAttachments = [];
    private uint _depthStencilWidth;
    private uint _depthStencilHeight;

    /// <inheritdoc/>
    public string Name => "DepthStencil";

    /// <inheritdoc/>
    public void Initialize(Window window)
    {
        _presenter = window.Presenter;
        var frameSlotCount = checked((int)_presenter.MaxFramesInFlight);
        _depthStencilTextures = new Texture?[frameSlotCount];
        _depthStencilAttachments = new TextureView?[frameSlotCount];
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
        foreach (var depthStencilTexture in _depthStencilTextures)
            depthStencilTexture?.Destroy();
        _indexBuffer.Destroy();
        _vertexBuffer.Destroy();
        _pipeline.Destroy();
        _fragShader.Destroy();
        _vertShader.Destroy();
    }

    private void DrawFrame()
    {
        EnsureDepthStencilAttachmentSize();

        if (!_presenter.TryBeginFrame(out var frame))
            return;

        _vertexUploadHandle.ThrowIfNotReady();
        _indexUploadHandle.ThrowIfNotReady();

        var depthStencilAttachment = EnsureDepthStencilAttachment(frame.FrameSlot);

        try
        {
            var commands = frame.CommandList;
            commands.BeginRecording();
            commands.BeginRendering(new RenderingDescription
            {
                Width = frame.Width,
                Height = frame.Height,
                ColorAttachments =
                [
                    new ColorAttachmentDescription(
                        frame.ColorOutput,
                        new ClearColor(0.01f, 0.01f, 0.015f, 1))
                ],
                DepthStencilAttachment = new DepthStencilAttachmentDescription(depthStencilAttachment)
                {
                    DepthClearValue = 1,
                    StencilClearValue = 0
                }
            });
            commands.SetGraphicsPipeline(_pipeline);
            commands.SetViewport(0, 0, frame.Width, frame.Height);
            commands.SetScissor(0, 0, frame.Width, frame.Height);
            commands.SetVertexBuffer(0, _vertexBuffer);
            commands.SetIndexBuffer(_indexBuffer, IndexFormat.UInt16);
            commands.DrawIndexed((uint)_indices.Length);
            commands.EndRendering();
            commands.PrepareForPresent(frame.ColorOutput);
            commands.EndRecording();
        }
        finally
        {
            _presenter.EndFrame(frame);
        }
    }

    private void EnsureDepthStencilAttachmentSize()
    {
        if (_depthStencilWidth != _presenter.Width || _depthStencilHeight != _presenter.Height)
        {
            ClientTestApp.Current.Device.WaitIdle();
            for (var i = 0; i < _depthStencilAttachments.Length; i++)
            {
                _depthStencilAttachments[i]?.Destroy();
                _depthStencilAttachments[i] = null;
                _depthStencilTextures[i]?.Destroy();
                _depthStencilTextures[i] = null;
            }

            _depthStencilWidth = _presenter.Width;
            _depthStencilHeight = _presenter.Height;
        }
    }

    private TextureView EnsureDepthStencilAttachment(uint frameSlot)
    {
        var frameIndex = checked((int)frameSlot);
        if (_depthStencilAttachments[frameIndex] is { } existingAttachment)
            return existingAttachment;

        var device = ClientTestApp.Current.Device;
        var texture = device.CreateTexture(new TextureDescription
        {
            Dimension = TextureDimension.Type2D,
            Format = TextureFormat.Depth24UnormStencil8,
            Width = _depthStencilWidth,
            Height = _depthStencilHeight,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            Usage = TextureUsage.DepthStencilAttachment
        });
        try
        {
            var attachment = device.CreateTextureView(texture, new TextureViewDescription(
                TextureViewDimension.Type2D,
                0,
                1,
                0,
                1));
            _depthStencilTextures[frameIndex] = texture;
            _depthStencilAttachments[frameIndex] = attachment;
            return attachment;
        }
        catch
        {
            texture.Destroy();
            throw;
        }
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

        _vertShader =
            ClientTestApp.Current.Device.CreateShader(new ShaderDescription(ShaderStage.Vertex, vertBytecode,
                "depth_stencil.vert"));
        _fragShader =
            ClientTestApp.Current.Device.CreateShader(new ShaderDescription(ShaderStage.Fragment, fragBytecode,
                "depth_stencil.frag"));
    }

    private void CreatePipeline(TextureFormat colorFormat)
    {
        _pipeline = ClientTestApp.Current.Device.CreateGraphicsPipeline(new GraphicsPipelineDescription
        {
            Shaders = [_vertShader, _fragShader],
            VertexInput = CreateVertexInputDescription(),
            ColorAttachmentFormats = [colorFormat],
            DescriptorSetLayouts = [],
            DepthStencil = new DepthStencilDescription(
                true,
                true,
                CompareOperation.LessOrEqual,
                true,
                new StencilFaceDescription(),
                new StencilFaceDescription()),
            DepthStencilAttachmentFormat = TextureFormat.Depth24UnormStencil8
        });
    }

    private static VertexInputDescription CreateVertexInputDescription()
    {
        return new VertexInputDescription(
            [new VertexBufferLayoutDescription(0, (uint)Marshal.SizeOf<Vertex>())],
            [
                new VertexAttributeDescription(0, 0, VertexAttributeFormat.Float32x3, 0),
                new VertexAttributeDescription(1, 0, VertexAttributeFormat.Float32x3, 12)
            ]);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct Vertex(float X, float Y, float Z, float R, float G, float B);
}

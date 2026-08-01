using System.Runtime.InteropServices;
using PanguEngine.Graphics;
using PanguEngine.Windowing;
using GraphicsBuffer = PanguEngine.Graphics.Buffer;

namespace PanguEngine.Client.Tests.MultiColorAttachment;

internal static class MultiColorAttachment
{
    private static void Main()
    {
        ClientTestApp.Run(new MultiColorAttachmentScene());
    }
}

internal sealed class MultiColorAttachmentScene : IClientTestScene
{
    private readonly Vertex[] _vertices =
    [
        new(0.0f, -0.6f, 0.95f, 0.2f, 0.1f),
        new(0.6f, 0.5f, 0.1f, 0.85f, 0.25f),
        new(-0.6f, 0.5f, 0.2f, 0.35f, 1.0f)
    ];

    private Shader _vertShader = null!;
    private Shader _fragShader = null!;
    private GraphicsPipeline _pipeline = null!;
    private GraphicsBuffer _vertexBuffer = null!;
    private UploadHandle _vertexUploadHandle = null!;
    private Presenter _presenter = null!;
    private Texture?[] _offscreenTextures = [];
    private TextureView?[] _offscreenAttachments = [];
    private uint _attachmentWidth;
    private uint _attachmentHeight;

    public string Name => "MultiColorAttachment";

    public void Initialize(Window window)
    {
        _presenter = window.Presenter;
        var frameSlotCount = checked((int)_presenter.MaxFramesInFlight);
        _offscreenTextures = new Texture?[frameSlotCount];
        _offscreenAttachments = new TextureView?[frameSlotCount];
        CreateBuffer();
        CreateShaders();
        CreatePipeline(_presenter.ColorFormat);
        window.Render += (_, _) => DrawFrame();
    }

    public void Destroy()
    {
        foreach (var attachment in _offscreenAttachments)
            attachment?.Destroy();
        foreach (var texture in _offscreenTextures)
            texture?.Destroy();
        _vertexBuffer.Destroy();
        _pipeline.Destroy();
        _fragShader.Destroy();
        _vertShader.Destroy();
    }

    private void DrawFrame()
    {
        EnsureOffscreenAttachmentSize();

        if (!_presenter.TryBeginFrame(out var frame))
            return;

        _vertexUploadHandle.ThrowIfNotReady();

        var offscreenAttachment = EnsureOffscreenAttachment(frame.FrameSlot);

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
                        new ClearColor(0.02f, 0.03f, 0.05f, 1)),
                    new ColorAttachmentDescription(offscreenAttachment, new ClearColor(0, 0, 0, 1))
                ]
            });
            commands.SetGraphicsPipeline(_pipeline);
            commands.SetViewport(0, 0, frame.Width, frame.Height);
            commands.SetScissor(0, 0, frame.Width, frame.Height);
            commands.SetVertexBuffer(0, _vertexBuffer);
            commands.Draw((uint)_vertices.Length);
            commands.EndRendering();
            commands.PrepareForPresent(frame.ColorOutput);
            commands.EndRecording();
        }
        finally
        {
            _presenter.EndFrame(frame);
        }
    }

    private void EnsureOffscreenAttachmentSize()
    {
        if (_attachmentWidth != _presenter.Width || _attachmentHeight != _presenter.Height)
        {
            ClientTestApp.Current.Device.WaitIdle();
            for (var i = 0; i < _offscreenAttachments.Length; i++)
            {
                _offscreenAttachments[i]?.Destroy();
                _offscreenAttachments[i] = null;
                _offscreenTextures[i]?.Destroy();
                _offscreenTextures[i] = null;
            }

            _attachmentWidth = _presenter.Width;
            _attachmentHeight = _presenter.Height;
        }
    }

    private TextureView EnsureOffscreenAttachment(uint frameSlot)
    {
        var frameIndex = checked((int)frameSlot);
        if (_offscreenAttachments[frameIndex] is { } existingAttachment)
            return existingAttachment;

        var device = ClientTestApp.Current.Device;
        var texture = device.CreateTexture(new TextureDescription
        {
            Dimension = TextureDimension.Type2D,
            Format = _presenter.ColorFormat,
            Width = _attachmentWidth,
            Height = _attachmentHeight,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            Usage = TextureUsage.ColorAttachment
        });
        try
        {
            var attachment = device.CreateTextureView(texture, new TextureViewDescription(
                TextureViewDimension.Type2D,
                0,
                1,
                0,
                1));
            _offscreenTextures[frameIndex] = texture;
            _offscreenAttachments[frameIndex] = attachment;
            return attachment;
        }
        catch
        {
            texture.Destroy();
            throw;
        }
    }

    private void CreateBuffer()
    {
        var vertexBufferSize = (ulong)(Marshal.SizeOf<Vertex>() * _vertices.Length);
        _vertexBuffer = ClientTestApp.Current.Device.CreateBuffer(new BufferDescription(
            vertexBufferSize,
            BufferUsage.TransferDestination | BufferUsage.Vertex,
            MemoryUsage.GpuOnly));
        _vertexUploadHandle = ClientTestApp.Current.Device.UploadBuffer(_vertexBuffer, _vertices);
    }

    private void CreateShaders()
    {
        var basePath = AppContext.BaseDirectory;
        var vertPath = Path.Combine(basePath, "shaders", "multi_color.vert");
        var fragPath = Path.Combine(basePath, "shaders", "multi_color.frag");

        var vertSource = File.ReadAllText(vertPath);
        var fragSource = File.ReadAllText(fragPath);

        var vertBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Vertex, vertSource, name: "multi_color.vert");
        var fragBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Fragment, fragSource, name: "multi_color.frag");

        _vertShader =
            ClientTestApp.Current.Device.CreateShader(new ShaderDescription(ShaderStage.Vertex, vertBytecode,
                "multi_color.vert"));
        _fragShader =
            ClientTestApp.Current.Device.CreateShader(new ShaderDescription(ShaderStage.Fragment, fragBytecode,
                "multi_color.frag"));
    }

    private void CreatePipeline(TextureFormat colorFormat)
    {
        _pipeline = ClientTestApp.Current.Device.CreateGraphicsPipeline(new GraphicsPipelineDescription
        {
            Shaders = [_vertShader, _fragShader],
            VertexInput = CreateVertexInputDescription(),
            ColorAttachmentFormats = [colorFormat, colorFormat],
            DescriptorSetLayouts = []
        });
    }

    private static VertexInputDescription CreateVertexInputDescription()
    {
        return new VertexInputDescription(
            [new VertexBufferLayoutDescription(0, (uint)Marshal.SizeOf<Vertex>())],
            [
                new VertexAttributeDescription(0, 0, VertexAttributeFormat.Float32x2, 0),
                new VertexAttributeDescription(1, 0, VertexAttributeFormat.Float32x3, 8)
            ]);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct Vertex(float X, float Y, float R, float G, float B);
}

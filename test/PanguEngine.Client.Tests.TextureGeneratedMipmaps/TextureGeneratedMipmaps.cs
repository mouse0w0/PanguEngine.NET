using System.Runtime.InteropServices;
using PanguEngine.Graphics;
using PanguEngine.Windowing;
using GraphicsBuffer = PanguEngine.Graphics.Buffer;

namespace PanguEngine.Client.Tests.TextureGeneratedMipmaps;

internal static class TextureGeneratedMipmaps
{
    private static void Main()
    {
        ClientTestApp.Run(new TextureGeneratedMipmapsScene());
    }
}

internal sealed class TextureGeneratedMipmapsScene : IClientTestScene
{
    private readonly Vertex[] _vertices =
    [
        new(-0.9f, -0.6f, 0, 0),
        new(0.9f, -0.6f, 3, 0),
        new(0.9f, 0.6f, 3, 1),
        new(-0.9f, -0.6f, 0, 0),
        new(0.9f, 0.6f, 3, 1),
        new(-0.9f, 0.6f, 0, 1),
    ];

    private Shader _vertShader = null!;
    private Shader _fragShader = null!;
    private DescriptorSetLayout _descriptorSetLayout = null!;
    private DescriptorSet _descriptorSet = null!;
    private GraphicsPipeline _pipeline = null!;
    private GraphicsBuffer _vertexBuffer = null!;
    private Texture _texture = null!;
    private Sampler _sampler = null!;
    private UploadHandle _vertexUploadHandle = null!;
    private UploadHandle _textureUploadHandle = null!;
    private UploadHandle _mipmapUploadHandle = null!;
    private Presenter _presenter = null!;

    public string Name => "TextureGeneratedMipmaps";

    public void Initialize(Window window)
    {
        _presenter = window.Presenter;
        CreateVertexBuffer();
        CreateTexture();
        CreateSampler();
        CreateDescriptorSetLayout();
        CreateDescriptorSet();
        CreateShaders();
        CreatePipeline(_presenter.ColorFormat);
        window.Render += (_, _) => DrawFrame();
    }

    public void Destroy()
    {
        _pipeline.Destroy();
        _descriptorSet.Destroy();
        _descriptorSetLayout.Destroy();
        _sampler.Destroy();
        _texture.Destroy();
        _fragShader.Destroy();
        _vertShader.Destroy();
        _vertexBuffer.Destroy();
    }

    private void DrawFrame()
    {
        if (!_presenter.TryBeginFrame(out var frame))
            return;

        if (!_vertexUploadHandle.CheckSuccess())
            throw new InvalidOperationException(
                "Vertex buffer upload did not complete after flushing pending uploads.");
        if (!_textureUploadHandle.CheckSuccess())
            throw new InvalidOperationException("Texture upload did not complete after flushing pending uploads.");
        if (!_mipmapUploadHandle.CheckSuccess())
            throw new InvalidOperationException("Mipmap generation did not complete after flushing pending uploads.");

        try
        {
            var commands = frame.CommandList;
            commands.BeginRecording();
            commands.BeginRendering(new RenderingDescription(
                frame.Width,
                frame.Height,
                [
                    new ColorAttachmentDescription(frame.ColorOutput, new ClearColor(0.01f, 0.01f, 0.015f, 1)),
                ]));
            commands.SetGraphicsPipeline(_pipeline);
            commands.SetViewport(0, 0, frame.Width, frame.Height);
            commands.SetScissor(0, 0, frame.Width, frame.Height);
            commands.SetVertexBuffer(0, _vertexBuffer);
            commands.SetDescriptorSet(0, _descriptorSet);
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

    private void CreateVertexBuffer()
    {
        var size = (ulong)(Marshal.SizeOf<Vertex>() * _vertices.Length);
        _vertexBuffer = ClientTestApp.Current.Device.CreateBuffer(new BufferDescription(
            size,
            BufferUsage.TransferDestination | BufferUsage.Vertex,
            MemoryUsage.GpuOnly));
        _vertexUploadHandle = ClientTestApp.Current.Device.UploadBuffer(_vertexBuffer, _vertices);
    }

    private void CreateTexture()
    {
        _texture = ClientTestApp.Current.Device.CreateTexture(new TextureDescription(
            TextureDimension.Type2D,
            TextureFormat.R8G8B8A8Unorm,
            4,
            4,
            1,
            3,
            1,
            TextureUsage.TransferSource | TextureUsage.TransferDestination | TextureUsage.Sampled));

        _textureUploadHandle = ClientTestApp.Current.Device.UploadTexture(
            _texture,
            CreateBlockTextureData(4, 4),
            TextureUploadRegion.Mip2D(4, 4, 0));
        _mipmapUploadHandle = ClientTestApp.Current.Device.GenerateMipmaps(_texture);
    }

    private void CreateSampler()
    {
        _sampler = ClientTestApp.Current.Device.CreateSampler(new SamplerDescription(
            FilterMode.Nearest,
            FilterMode.Nearest,
            MipmapMode.Nearest,
            WrapMode.ClampToEdge,
            WrapMode.ClampToEdge,
            WrapMode.ClampToEdge,
            0,
            0,
            2,
            0));
    }

    private void CreateDescriptorSetLayout()
    {
        _descriptorSetLayout = ClientTestApp.Current.Device.CreateDescriptorSetLayout(
            new DescriptorSetLayoutDescription(
            [
                new DescriptorSetLayoutBinding(0, DescriptorType.CombinedImageSampler, ShaderStage.Fragment)
            ]));
    }

    private void CreateDescriptorSet()
    {
        _descriptorSet = ClientTestApp.Current.Device.CreateDescriptorSet(new DescriptorSetDescription(
            _descriptorSetLayout,
            [
                DescriptorSetBinding.CombinedImageSampler(0, _texture, _sampler)
            ]));
    }

    private void CreateShaders()
    {
        var basePath = AppContext.BaseDirectory;
        var vertPath = Path.Combine(basePath, "shaders", "texture_generated_mipmaps.vert");
        var fragPath = Path.Combine(basePath, "shaders", "texture_generated_mipmaps.frag");

        var vertBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Vertex, File.ReadAllText(vertPath),
            name: "texture_generated_mipmaps.vert");
        var fragBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Fragment, File.ReadAllText(fragPath),
            name: "texture_generated_mipmaps.frag");

        _vertShader = ClientTestApp.Current.Device.CreateShader(new ShaderDescription(
            ShaderStage.Vertex,
            vertBytecode,
            Name: "texture_generated_mipmaps.vert"));
        _fragShader = ClientTestApp.Current.Device.CreateShader(new ShaderDescription(
            ShaderStage.Fragment,
            fragBytecode,
            Name: "texture_generated_mipmaps.frag"));
    }

    private void CreatePipeline(TextureFormat colorFormat)
    {
        _pipeline = ClientTestApp.Current.Device.CreateGraphicsPipeline(new GraphicsPipelineDescription(
            [_vertShader, _fragShader],
            CreateVertexInputDescription(),
            ColorAttachmentFormats: [colorFormat],
            DescriptorSetLayouts: [_descriptorSetLayout]));
    }

    private static VertexInputDescription CreateVertexInputDescription()
    {
        return new VertexInputDescription(
            [
                new VertexBufferLayoutDescription(0, (uint)Marshal.SizeOf<Vertex>())
            ],
            [
                new VertexAttributeDescription(0, 0, VertexAttributeFormat.Float32x2, 0),
                new VertexAttributeDescription(1, 0, VertexAttributeFormat.Float32x2, 8)
            ]);
    }

    private static byte[] CreateBlockTextureData(uint width, uint height)
    {
        var data = new byte[checked((int)(width * height * 4))];
        ReadOnlySpan<byte> colors =
        [
            0xff, 0x20, 0x20, 0xff,
            0xff, 0x80, 0x20, 0xff,
            0xff, 0xe0, 0x20, 0xff,
            0x80, 0xff, 0x20, 0xff,
            0x20, 0xff, 0x20, 0xff,
            0x20, 0xff, 0x80, 0xff,
            0x20, 0xff, 0xff, 0xff,
            0x20, 0x80, 0xff, 0xff,
            0x20, 0x20, 0xff, 0xff,
            0x80, 0x20, 0xff, 0xff,
            0xff, 0x20, 0xff, 0xff,
            0xff, 0x20, 0x80, 0xff,
            0xff, 0xff, 0xff, 0xff,
            0xc0, 0xc0, 0xc0, 0xff,
            0x80, 0x80, 0x80, 0xff,
            0x20, 0x20, 0x20, 0xff,
        ];

        for (var y = 0u; y < height; y++)
        {
            for (var x = 0u; x < width; x++)
            {
                var offset = checked((int)((y * width + x) * 4));
                var tileX = Math.Min(x, 3);
                var tileY = Math.Min(y, 3);
                var colorOffset = checked((int)((tileY * 4 + tileX) * 4));
                data[offset] = colors[colorOffset];
                data[offset + 1] = colors[colorOffset + 1];
                data[offset + 2] = colors[colorOffset + 2];
                data[offset + 3] = colors[colorOffset + 3];
            }
        }

        return data;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct Vertex(float X, float Y, float U, float V);
}
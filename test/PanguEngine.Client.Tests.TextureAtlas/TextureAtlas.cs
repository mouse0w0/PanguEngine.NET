using System.Runtime.InteropServices;
using PanguEngine.Graphics;
using PanguEngine.Windowing;
using GraphicsBuffer = PanguEngine.Graphics.Buffer;

namespace PanguEngine.Client.Tests.TextureAtlas;

internal static class TextureAtlas
{
    private static void Main()
    {
        ClientTestApp.Run(new TextureAtlasScene());
    }
}

internal sealed class TextureAtlasScene : IClientTestScene
{
    private const int ImageCount = 12;
    private const int MaxAtlasWidth = 512;
    private const int MaxAtlasHeight = 512;
    private const int Gutter = 2;
    private const float DisplayHalfExtent = 0.8f;

    private Vertex[] _vertices = [];
    private Shader _vertShader = null!;
    private Shader _fragShader = null!;
    private DescriptorSetLayout _descriptorSetLayout = null!;
    private DescriptorSet _descriptorSet = null!;
    private GraphicsPipeline _pipeline = null!;
    private GraphicsBuffer _vertexBuffer = null!;
    private Texture _texture = null!;
    private TextureView _textureView = null!;
    private Sampler _sampler = null!;
    private UploadHandle _vertexUploadHandle = null!;
    private UploadHandle _textureUploadHandle = null!;
    private Presenter _presenter = null!;

    public string Name => "TextureAtlas";

    public void Initialize(Window window)
    {
        _presenter = window.Presenter;
        var atlas = CreateAtlas();
        _vertices = CreateVertices(atlas.Width, atlas.Height);
        CreateVertexBuffer();
        CreateTexture(atlas.Width, atlas.Height, atlas.Pixels.Span);
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
        _textureView.Destroy();
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
            throw new InvalidOperationException(
                "Texture upload did not complete after flushing pending uploads.");

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
                        new ClearColor(0.025f, 0.025f, 0.035f, 1))
                ]
            });
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

    private static TextureAtlas<string> CreateAtlas()
    {
        var builder = new MaxRectsTextureAtlasBuilder<string>(
            MaxAtlasWidth,
            MaxAtlasHeight,
            Gutter);
        var contentArea = 0L;
        var layoutArea = 0L;
        for (var index = 0; index < ImageCount; index++)
        {
            var width = 24 + index * 37 % 88;
            var height = 24 + index * 53 % 88;
            var red = (byte)(64 + index * 67 % 192);
            var green = (byte)(64 + index * 97 % 192);
            var blue = (byte)(64 + index * 131 % 192);
            contentArea = checked(contentArea + (long)width * height);
            layoutArea = checked(layoutArea
                                 + (width + 2L * Gutter) * (height + 2L * Gutter));
            builder.Add(
                $"image-{index}",
                width,
                height,
                CreateSolidPixels(width, height, red, green, blue, 0xff));
        }

        var atlas = builder.Build();
        WriteAtlasStatistics(atlas.Width, atlas.Height, contentArea, layoutArea);
        return atlas;
    }

    private static void WriteAtlasStatistics(
        int width,
        int height,
        long contentArea,
        long layoutArea)
    {
        var atlasArea = (long)width * height;
        var contentUtilization = contentArea * 100d / atlasArea;
        var layoutUtilization = layoutArea * 100d / atlasArea;
        Console.WriteLine($"Texture atlas: {width}x{height}");
        Console.WriteLine($"Content utilization: {contentUtilization:F2}%");
        Console.WriteLine($"Layout utilization: {layoutUtilization:F2}%");
    }

    private static Vertex[] CreateVertices(int atlasWidth, int atlasHeight)
    {
        var aspect = atlasWidth / (float)atlasHeight;
        var halfWidth = aspect >= 1 ? DisplayHalfExtent : DisplayHalfExtent * aspect;
        var halfHeight = aspect >= 1 ? DisplayHalfExtent / aspect : DisplayHalfExtent;
        return
        [
            new Vertex(-halfWidth, -halfHeight, 0, 0),
            new Vertex(halfWidth, -halfHeight, 1, 0),
            new Vertex(halfWidth, halfHeight, 1, 1),
            new Vertex(-halfWidth, -halfHeight, 0, 0),
            new Vertex(halfWidth, halfHeight, 1, 1),
            new Vertex(-halfWidth, halfHeight, 0, 1)
        ];
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

    private void CreateTexture(int width, int height, ReadOnlySpan<byte> pixels)
    {
        _texture = ClientTestApp.Current.Device.CreateTexture(new TextureDescription
        {
            Dimension = TextureDimension.Type2D,
            Format = TextureFormat.R8G8B8A8Unorm,
            Width = checked((uint)width),
            Height = checked((uint)height),
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            Usage = TextureUsage.TransferDestination | TextureUsage.Sampled
        });
        _textureView = ClientTestApp.Current.Device.CreateTextureView(_texture, new TextureViewDescription(
            TextureViewDimension.Type2D,
            0,
            1,
            0,
            1));
        _textureUploadHandle = ClientTestApp.Current.Device.UploadTexture(_texture, pixels);
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
            0,
            0));
    }

    private void CreateDescriptorSetLayout()
    {
        _descriptorSetLayout = ClientTestApp.Current.Device.CreateDescriptorSetLayout(
            new DescriptorSetLayoutDescription(
            [
                new DescriptorSetLayoutBinding(0, DescriptorType.CombinedImageSampler, ShaderStageFlags.Fragment)
            ]));
    }

    private void CreateDescriptorSet()
    {
        _descriptorSet = ClientTestApp.Current.Device.CreateDescriptorSet(new DescriptorSetDescription(
            _descriptorSetLayout,
            [
                DescriptorSetBinding.CombinedImageSampler(0, _textureView, _sampler)
            ]));
    }

    private void CreateShaders()
    {
        var basePath = AppContext.BaseDirectory;
        var vertPath = Path.Combine(basePath, "shaders", "texture_atlas.vert");
        var fragPath = Path.Combine(basePath, "shaders", "texture_atlas.frag");
        var vertBytecode = ShaderCompiler.CompileGlsl(
            ShaderStage.Vertex,
            File.ReadAllText(vertPath),
            name: "texture_atlas.vert");
        var fragBytecode = ShaderCompiler.CompileGlsl(
            ShaderStage.Fragment,
            File.ReadAllText(fragPath),
            name: "texture_atlas.frag");
        _vertShader = ClientTestApp.Current.Device.CreateShader(new ShaderDescription(
            ShaderStage.Vertex,
            vertBytecode,
            "texture_atlas.vert"));
        _fragShader = ClientTestApp.Current.Device.CreateShader(new ShaderDescription(
            ShaderStage.Fragment,
            fragBytecode,
            "texture_atlas.frag"));
    }

    private void CreatePipeline(TextureFormat colorFormat)
    {
        _pipeline = ClientTestApp.Current.Device.CreateGraphicsPipeline(new GraphicsPipelineDescription
        {
            Shaders = [_vertShader, _fragShader],
            VertexInput = CreateVertexInputDescription(),
            ColorAttachmentFormats = [colorFormat],
            DescriptorSetLayouts = [_descriptorSetLayout]
        });
    }

    private static VertexInputDescription CreateVertexInputDescription()
    {
        return new VertexInputDescription(
            [new VertexBufferLayoutDescription(0, (uint)Marshal.SizeOf<Vertex>())],
            [
                new VertexAttributeDescription(0, 0, VertexAttributeFormat.Float32x2, 0),
                new VertexAttributeDescription(1, 0, VertexAttributeFormat.Float32x2, 8)
            ]);
    }

    private static byte[] CreateSolidPixels(
        int width,
        int height,
        byte red,
        byte green,
        byte blue,
        byte alpha)
    {
        var pixels = new byte[checked(width * height * 4)];
        for (var offset = 0; offset < pixels.Length; offset += 4)
        {
            pixels[offset] = red;
            pixels[offset + 1] = green;
            pixels[offset + 2] = blue;
            pixels[offset + 3] = alpha;
        }

        return pixels;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct Vertex(float X, float Y, float U, float V);
}
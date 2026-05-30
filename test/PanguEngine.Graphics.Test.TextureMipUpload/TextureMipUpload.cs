using System.Runtime.InteropServices;
using PanguEngine.Windowing;
using GraphicsBuffer = PanguEngine.Graphics.Buffer;

namespace PanguEngine.Graphics.Test.TextureMipUpload;

internal static class TextureMipUpload
{
    private static void Main()
    {
        new GraphicsTestApp(new TextureMipUploadScene()).Run();
    }
}

internal sealed class TextureMipUploadScene : IGraphicsTestScene
{
    private readonly Vertex[] _vertices =
    [
        new(-0.9f, -0.6f, 0, 0),
        new(0.9f, -0.6f, 1, 0),
        new(0.9f, 0.6f, 1, 1),
        new(-0.9f, -0.6f, 0, 0),
        new(0.9f, 0.6f, 1, 1),
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
    private UploadHandle[] _textureUploadHandles = [];

    public string Name => "TextureMipUpload";

    public void Initialize(Window window)
    {
        CreateVertexBuffer();
        CreateTexture();
        CreateSampler();
        CreateDescriptorSetLayout();
        CreateDescriptorSet();
        CreateShaders();
        CreatePipeline(window.Presenter.ColorFormat);
    }

    public void Record(Frame frame, CommandList commands)
    {
        if (!_vertexUploadHandle.IsCompleted)
            throw new InvalidOperationException(
                "Vertex buffer upload did not complete after flushing pending uploads.");
        foreach (var uploadHandle in _textureUploadHandles)
        {
            if (!uploadHandle.IsCompleted)
                throw new InvalidOperationException(
                    "Texture upload did not complete after flushing pending uploads.");
        }

        commands.BeginRendering(new RenderingDescription(new ClearColor(0.01f, 0.01f, 0.015f, 1)));
        commands.SetGraphicsPipeline(_pipeline);
        commands.SetViewport(0, 0, frame.Width, frame.Height);
        commands.SetScissor(0, 0, frame.Width, frame.Height);
        commands.SetVertexBuffer(0, _vertexBuffer);
        commands.SetDescriptorSet(0, _descriptorSet);
        commands.Draw((uint)_vertices.Length);
        commands.EndRendering();
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

    private void CreateVertexBuffer()
    {
        var size = (ulong)(Marshal.SizeOf<Vertex>() * _vertices.Length);
        _vertexBuffer = GraphicsContext.Device.CreateBuffer(new BufferDescription(
            size,
            BufferUsage.TransferDestination | BufferUsage.Vertex,
            MemoryUsage.GpuOnly));
        _vertexUploadHandle = GraphicsContext.Device.UploadBuffer(_vertexBuffer, _vertices);
    }

    private void CreateTexture()
    {
        _texture = GraphicsContext.Device.CreateTexture(new TextureDescription(
            TextureDimension.Type2D,
            TextureFormat.R8G8B8A8Unorm,
            8,
            8,
            1,
            3,
            1,
            TextureUsage.TransferDestination | TextureUsage.Sampled));

        _textureUploadHandles =
        [
            GraphicsContext.Device.UploadTexture(_texture, CreateSolidTextureData(8, 8, 0xff, 0x00, 0x00, 0xff),
                TextureUploadRegion.Mip2D(8, 8, 0)),
            GraphicsContext.Device.UploadTexture(_texture, CreateSolidTextureData(4, 4, 0x00, 0xff, 0x00, 0xff),
                TextureUploadRegion.Mip2D(4, 4, 1)),
            GraphicsContext.Device.UploadTexture(_texture, CreateSolidTextureData(2, 2, 0x00, 0x00, 0xff, 0xff),
                TextureUploadRegion.Mip2D(2, 2, 2)),
        ];
    }

    private void CreateSampler()
    {
        _sampler = GraphicsContext.Device.CreateSampler(new SamplerDescription(
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
        _descriptorSetLayout = GraphicsContext.Device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription(
            new[]
            {
                new DescriptorSetLayoutBinding(0, DescriptorType.CombinedImageSampler, ShaderStage.Fragment)
            }));
    }

    private void CreateDescriptorSet()
    {
        _descriptorSet = GraphicsContext.Device.CreateDescriptorSet(new DescriptorSetDescription(
            _descriptorSetLayout,
            new[]
            {
                DescriptorSetBinding.CombinedImageSampler(0, _texture, _sampler)
            }));
    }

    private void CreateShaders()
    {
        var basePath = AppContext.BaseDirectory;
        var vertPath = Path.Combine(basePath, "Shaders", "texture_mip_upload.vert");
        var fragPath = Path.Combine(basePath, "Shaders", "texture_mip_upload.frag");

        var vertBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Vertex, File.ReadAllText(vertPath),
            name: "texture_mip_upload.vert");
        var fragBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Fragment, File.ReadAllText(fragPath),
            name: "texture_mip_upload.frag");

        _vertShader = GraphicsContext.Device.CreateShader(new ShaderDescription(
            ShaderStage.Vertex,
            vertBytecode,
            Name: "texture_mip_upload.vert"));
        _fragShader = GraphicsContext.Device.CreateShader(new ShaderDescription(
            ShaderStage.Fragment,
            fragBytecode,
            Name: "texture_mip_upload.frag"));
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
                new VertexAttributeDescription(1, 0, VertexAttributeFormat.Float32x2, 8)
            });
    }

    private static byte[] CreateSolidTextureData(uint width, uint height, byte r, byte g, byte b, byte a)
    {
        var data = new byte[checked((int)(width * height * 4))];
        for (var i = 0; i < data.Length; i += 4)
        {
            data[i] = r;
            data[i + 1] = g;
            data[i + 2] = b;
            data[i + 3] = a;
        }

        return data;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct Vertex(float X, float Y, float U, float V);
}
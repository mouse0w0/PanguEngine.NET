using System.Runtime.InteropServices;
using PanguEngine.Graphics;
using PanguEngine.Windowing;
using GraphicsBuffer = PanguEngine.Graphics.Buffer;

namespace PanguEngine.Client.Tests.TextureCubeMapUpload;

internal static class TextureCubeMapUpload
{
    private static void Main()
    {
        ClientTestApp.Run(new TextureCubeMapUploadScene());
    }
}

internal sealed class TextureCubeMapUploadScene : IClientTestScene
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
    private Presenter _presenter = null!;

    public string Name => "TextureCubeMapUpload";

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

        if (!_vertexUploadHandle.IsCompleted)
            throw new InvalidOperationException(
                "Vertex buffer upload did not complete after flushing pending uploads.");
        foreach (var uploadHandle in _textureUploadHandles)
        {
            if (!uploadHandle.IsCompleted)
                throw new InvalidOperationException(
                    "Texture upload did not complete after flushing pending uploads.");
        }

        try
        {
            var commands = frame.CommandList;
            commands.Begin();
            commands.BeginRendering(new RenderingDescription([
                new ColorAttachmentDescription(frame.ColorOutput, new ClearColor(0.01f, 0.01f, 0.015f, 1)),
            ]));
            commands.SetGraphicsPipeline(_pipeline);
            commands.SetViewport(0, 0, frame.Width, frame.Height);
            commands.SetScissor(0, 0, frame.Width, frame.Height);
            commands.SetVertexBuffer(0, _vertexBuffer);
            commands.SetDescriptorSet(0, _descriptorSet);
            commands.Draw((uint)_vertices.Length);
            commands.EndRendering();
            commands.PrepareForPresent();
            commands.End();
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
            TextureDimension.CubeMap,
            TextureFormat.R8G8B8A8Unorm,
            4,
            4,
            1,
            1,
            6,
            TextureUsage.TransferDestination | TextureUsage.Sampled));

        _textureUploadHandles =
        [
            ClientTestApp.Current.Device.UploadTexture(_texture, CreateSolidTextureData(4, 4, 0xff, 0x00, 0x00, 0xff),
                TextureUploadRegion.Layer2D(4, 4, 0)),
            ClientTestApp.Current.Device.UploadTexture(_texture, CreateSolidTextureData(4, 4, 0x00, 0xff, 0x00, 0xff),
                TextureUploadRegion.Layer2D(4, 4, 1)),
            ClientTestApp.Current.Device.UploadTexture(_texture, CreateSolidTextureData(4, 4, 0x00, 0x00, 0xff, 0xff),
                TextureUploadRegion.Layer2D(4, 4, 2)),
            ClientTestApp.Current.Device.UploadTexture(_texture, CreateSolidTextureData(4, 4, 0xff, 0xff, 0x00, 0xff),
                TextureUploadRegion.Layer2D(4, 4, 3)),
            ClientTestApp.Current.Device.UploadTexture(_texture, CreateSolidTextureData(4, 4, 0xff, 0x00, 0xff, 0xff),
                TextureUploadRegion.Layer2D(4, 4, 4)),
            ClientTestApp.Current.Device.UploadTexture(_texture, CreateSolidTextureData(4, 4, 0x00, 0xff, 0xff, 0xff),
                TextureUploadRegion.Layer2D(4, 4, 5)),
        ];
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
        var vertPath = Path.Combine(basePath, "shaders", "texture_cube_map_upload.vert");
        var fragPath = Path.Combine(basePath, "shaders", "texture_cube_map_upload.frag");

        var vertBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Vertex, File.ReadAllText(vertPath),
            name: "texture_cube_map_upload.vert");
        var fragBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Fragment, File.ReadAllText(fragPath),
            name: "texture_cube_map_upload.frag");

        _vertShader = ClientTestApp.Current.Device.CreateShader(new ShaderDescription(
            ShaderStage.Vertex,
            vertBytecode,
            Name: "texture_cube_map_upload.vert"));
        _fragShader = ClientTestApp.Current.Device.CreateShader(new ShaderDescription(
            ShaderStage.Fragment,
            fragBytecode,
            Name: "texture_cube_map_upload.frag"));
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
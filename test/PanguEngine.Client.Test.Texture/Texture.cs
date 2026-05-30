using System.Runtime.InteropServices;
using PanguEngine.Graphics;
using PanguEngine.Windowing;
using StbImageSharp;
using GraphicsBuffer = PanguEngine.Graphics.Buffer;

namespace PanguEngine.Client.Test.Texture;

internal static class Texture
{
    private static void Main()
    {
        ClientTestApp.Run(new TextureScene());
    }
}

internal sealed class TextureScene : IClientTestScene
{
    private readonly Vertex[] _vertices =
    [
        new(-0.6f, -0.6f, 0, 0),
        new(0.6f, -0.6f, 1, 0),
        new(0.6f, 0.6f, 1, 1),
        new(-0.6f, -0.6f, 0, 0),
        new(0.6f, 0.6f, 1, 1),
        new(-0.6f, 0.6f, 0, 1),
    ];

    private Shader _vertShader = null!;
    private Shader _fragShader = null!;
    private DescriptorSetLayout _descriptorSetLayout = null!;
    private DescriptorSet _descriptorSet = null!;
    private GraphicsPipeline _pipeline = null!;
    private GraphicsBuffer _vertexBuffer = null!;
    private Graphics.Texture _texture = null!;
    private Sampler _sampler = null!;
    private UploadHandle _vertexUploadHandle = null!;
    private UploadHandle _textureUploadHandle = null!;
    private Presenter _presenter = null!;

    public string Name => "Texture";

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
        if (!_textureUploadHandle.IsCompleted)
            throw new InvalidOperationException(
                "Texture upload did not complete after flushing pending uploads.");

        var activeFrame = frame!;
        try
        {
            var commands = activeFrame.CommandList;
            commands.Begin();
            commands.BeginRendering(new RenderingDescription(new ClearColor(0.01f, 0.01f, 0.015f, 1)));
            commands.SetGraphicsPipeline(_pipeline);
            commands.SetViewport(0, 0, activeFrame.Width, activeFrame.Height);
            commands.SetScissor(0, 0, activeFrame.Width, activeFrame.Height);
            commands.SetVertexBuffer(0, _vertexBuffer);
            commands.SetDescriptorSet(0, _descriptorSet);
            commands.Draw((uint)_vertices.Length);
            commands.EndRendering();
            commands.End();
        }
        finally
        {
            _presenter.EndFrame(activeFrame);
        }
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
        var path = Path.Combine(AppContext.BaseDirectory, "Textures", "texture.png");
        using var stream = File.OpenRead(path);
        var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        var width = checked((uint)image.Width);
        var height = checked((uint)image.Height);

        _texture = GraphicsContext.Device.CreateTexture(new TextureDescription(
            TextureDimension.Type2D,
            TextureFormat.R8G8B8A8Unorm,
            width,
            height,
            1,
            1,
            1,
            TextureUsage.TransferDestination | TextureUsage.Sampled));
        _textureUploadHandle = GraphicsContext.Device.UploadTexture(_texture, image.Data);
    }

    private void CreateSampler()
    {
        _sampler = GraphicsContext.Device.CreateSampler(new SamplerDescription(
            FilterMode.Linear,
            FilterMode.Linear,
            MipmapMode.Linear,
            WrapMode.Repeat,
            WrapMode.Repeat,
            WrapMode.Repeat,
            0,
            0,
            0,
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
        var vertPath = Path.Combine(basePath, "Shaders", "texture.vert");
        var fragPath = Path.Combine(basePath, "Shaders", "texture.frag");

        var vertSource = File.ReadAllText(vertPath);
        var fragSource = File.ReadAllText(fragPath);

        var vertBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Vertex, vertSource, name: "texture.vert");
        var fragBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Fragment, fragSource, name: "texture.frag");

        _vertShader = GraphicsContext.Device.CreateShader(new ShaderDescription(
            ShaderStage.Vertex,
            vertBytecode,
            Name: "texture.vert"));
        _fragShader = GraphicsContext.Device.CreateShader(new ShaderDescription(
            ShaderStage.Fragment,
            fragBytecode,
            Name: "texture.frag"));
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

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct Vertex(float X, float Y, float U, float V);
}
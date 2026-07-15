using PanguEngine.Graphics;
using PanguEngine.Windowing;

namespace PanguEngine.Client.Tests.Triangle;

/// <summary>
/// Entry point for the triangle graphics test.
/// </summary>
internal static class Triangle
{
    private static void Main()
    {
        ClientTestApp.Run(new TriangleScene());
    }
}

/// <summary>
/// Renders a static triangle without vertex buffers.
/// </summary>
internal sealed class TriangleScene : IClientTestScene
{
    private Shader _vertShader = null!;
    private Shader _fragShader = null!;
    private GraphicsPipeline _pipeline = null!;
    private Presenter _presenter = null!;

    /// <inheritdoc/>
    public string Name => "Triangle";

    /// <inheritdoc/>
    public void Initialize(Window window)
    {
        _presenter = window.Presenter;

        var basePath = AppContext.BaseDirectory;
        var vertPath = Path.Combine(basePath, "shaders", "triangle.vert");
        var fragPath = Path.Combine(basePath, "shaders", "triangle.frag");

        var vertSource = File.ReadAllText(vertPath);
        var fragSource = File.ReadAllText(fragPath);

        var vertBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Vertex, vertSource, name: "triangle.vert");
        var fragBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Fragment, fragSource, name: "triangle.frag");

        _vertShader =
            ClientTestApp.Current.Device.CreateShader(new ShaderDescription(ShaderStage.Vertex, vertBytecode,
                "triangle.vert"));
        _fragShader =
            ClientTestApp.Current.Device.CreateShader(new ShaderDescription(ShaderStage.Fragment, fragBytecode,
                "triangle.frag"));

        _pipeline = ClientTestApp.Current.Device.CreateGraphicsPipeline(new GraphicsPipelineDescription
        {
            Shaders = [_vertShader, _fragShader],
            VertexInput = VertexInputDescription.Empty,
            ColorAttachmentFormats = [_presenter.ColorFormat],
            DescriptorSetLayouts = []
        });

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
        if (!_presenter.TryBeginFrame(out var frame))
            return;

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
                    new ColorAttachmentDescription(frame.ColorOutput, new ClearColor(0, 0, 0, 1))
                ]
            });
            commands.SetGraphicsPipeline(_pipeline);
            commands.SetViewport(0, 0, frame.Width, frame.Height);
            commands.SetScissor(0, 0, frame.Width, frame.Height);
            commands.Draw(3);
            commands.EndRendering();
            commands.PrepareForPresent(frame.ColorOutput);
            commands.EndRecording();
        }
        finally
        {
            _presenter.EndFrame(frame);
        }
    }
}
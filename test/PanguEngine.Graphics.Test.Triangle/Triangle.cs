using PanguEngine.Windowing;

namespace PanguEngine.Graphics.Test.Triangle;

/// <summary>
/// Entry point for the triangle graphics test.
/// </summary>
internal static class Triangle
{
    private static void Main()
    {
        new GraphicsTestApp(new TriangleScene()).Run();
    }
}

/// <summary>
/// Renders a static triangle without vertex buffers.
/// </summary>
internal sealed class TriangleScene : IGraphicsTestScene
{
    private Shader _vertShader = null!;
    private Shader _fragShader = null!;
    private GraphicsPipeline _pipeline = null!;

    /// <inheritdoc/>
    public string Name => "Triangle";

    /// <inheritdoc/>
    public void Initialize(Window window)
    {
        var basePath = AppContext.BaseDirectory;
        var vertPath = Path.Combine(basePath, "Shaders", "triangle.vert");
        var fragPath = Path.Combine(basePath, "Shaders", "triangle.frag");

        var vertSource = File.ReadAllText(vertPath);
        var fragSource = File.ReadAllText(fragPath);

        var vertBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Vertex, vertSource, name: "triangle.vert");
        var fragBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Fragment, fragSource, name: "triangle.frag");

        _vertShader = GraphicsContext.Device.CreateShader(new ShaderDescription(
            ShaderStage.Vertex,
            vertBytecode,
            Name: "triangle.vert"));
        _fragShader = GraphicsContext.Device.CreateShader(new ShaderDescription(
            ShaderStage.Fragment,
            fragBytecode,
            Name: "triangle.frag"));

        _pipeline = GraphicsContext.Device.CreateGraphicsPipeline(new GraphicsPipelineDescription(
            new[] { _vertShader, _fragShader },
            VertexInputDescription.Empty,
            ColorAttachmentFormat: window.Presenter.ColorFormat));
    }

    /// <inheritdoc/>
    public void Record(Frame frame, CommandList commands)
    {
        commands.BeginRendering(new RenderingDescription(new ClearColor(0, 0, 0, 1)));
        commands.SetGraphicsPipeline(_pipeline);
        commands.SetViewport(0, 0, frame.Width, frame.Height);
        commands.SetScissor(0, 0, frame.Width, frame.Height);
        commands.Draw(3);
        commands.EndRendering();
    }

    /// <inheritdoc/>
    public void Destroy()
    {
        _pipeline.Destroy();
        _fragShader.Destroy();
        _vertShader.Destroy();
    }
}
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

        _vertShader = ClientTestApp.Instance.Device.CreateShader(new ShaderDescription(
            ShaderStage.Vertex,
            vertBytecode,
            Name: "triangle.vert"));
        _fragShader = ClientTestApp.Instance.Device.CreateShader(new ShaderDescription(
            ShaderStage.Fragment,
            fragBytecode,
            Name: "triangle.frag"));

        _pipeline = ClientTestApp.Instance.Device.CreateGraphicsPipeline(new GraphicsPipelineDescription(
            new[] { _vertShader, _fragShader },
            VertexInputDescription.Empty,
            ColorAttachmentFormat: _presenter.ColorFormat));

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

        var activeFrame = frame!;
        try
        {
            var commands = activeFrame.CommandList;
            commands.Begin();
            commands.BeginRendering(new RenderingDescription(new ClearColor(0, 0, 0, 1)));
            commands.SetGraphicsPipeline(_pipeline);
            commands.SetViewport(0, 0, activeFrame.Width, activeFrame.Height);
            commands.SetScissor(0, 0, activeFrame.Width, activeFrame.Height);
            commands.Draw(3);
            commands.EndRendering();
            commands.End();
        }
        finally
        {
            _presenter.EndFrame(activeFrame);
        }
    }
}
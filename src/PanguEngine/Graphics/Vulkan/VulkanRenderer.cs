namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Handles Vulkan graphics pipeline creation, command recording, and frame presentation.
/// </summary>
internal sealed unsafe class VulkanRenderer
{
    private readonly VulkanPresenter _presenter;

    private readonly Shader _vertexShader;
    private readonly Shader _fragmentShader;
    private readonly GraphicsPipeline _pipeline;

    /// <summary>
    /// Initializes the renderer by loading shaders and creating the graphics pipeline.
    /// </summary>
    /// <param name="presenter">The Vulkan presenter used for frame presentation.</param>
    internal VulkanRenderer(VulkanPresenter presenter)
    {
        _presenter = presenter;

        var basePath = AppContext.BaseDirectory;
        var vertPath = Path.Combine(basePath, "Assets", "Shaders", "triangle.vert");
        var fragPath = Path.Combine(basePath, "Assets", "Shaders", "triangle.frag");

        var vertSource = File.ReadAllText(vertPath);
        var fragSource = File.ReadAllText(fragPath);

        var vertBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Vertex, vertSource, name: "triangle.vert");
        var fragBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Fragment, fragSource, name: "triangle.frag");

        _vertexShader = GraphicsContext.Device.CreateShader(new ShaderDescription(
            ShaderStage.Vertex,
            vertBytecode,
            Name: "triangle.vert"));
        _fragmentShader = GraphicsContext.Device.CreateShader(new ShaderDescription(
            ShaderStage.Fragment,
            fragBytecode,
            Name: "triangle.frag"));
        _pipeline = GraphicsContext.Device.CreateGraphicsPipeline(new GraphicsPipelineDescription(
            new[] { _vertexShader, _fragmentShader },
            VertexInputDescription.Empty,
            ColorAttachmentFormat: _presenter.ColorFormat));
    }

    /// <summary>
    /// Records and submits a draw command for the current frame, then presents the rendered image.
    /// </summary>
    /// <param name="delta">The time elapsed since the last frame, in seconds.</param>
    public void DrawFrame(double delta)
    {
        var frame = _presenter.BeginFrame();
        try
        {
            var commandList = frame.CommandList;
            commandList.Begin();
            commandList.BeginRendering(new RenderingDescription(new ClearColor(0, 0, 0, 1)));
            commandList.SetGraphicsPipeline(_pipeline);
            commandList.SetViewport(0, 0, frame.Width, frame.Height);
            commandList.SetScissor(0, 0, frame.Width, frame.Height);
            commandList.Draw(3);
            commandList.EndRendering();
            commandList.End();
        }
        finally
        {
            _presenter.EndFrame(frame);
        }
    }

    /// <summary>
    /// Destroys the graphics pipeline and shader modules, releasing all GPU resources.
    /// </summary>
    public void Destroy()
    {
        VulkanContext.Vk.DeviceWaitIdle(VulkanContext.Device);

        _pipeline.Destroy();
        _fragmentShader.Destroy();
        _vertexShader.Destroy();
    }
}
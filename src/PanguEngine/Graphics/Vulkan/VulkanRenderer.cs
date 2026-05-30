namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Handles Vulkan graphics pipeline creation, command recording, and frame presentation.
/// </summary>
internal sealed class VulkanRenderer
{
    private readonly GraphicsDevice _device;
    private readonly Presenter _presenter;
    private readonly Shader _vertexShader;
    private readonly Shader _fragmentShader;
    private readonly GraphicsPipeline _pipeline;

    /// <summary>
    /// Initializes the renderer by loading shaders and creating the graphics pipeline.
    /// </summary>
    /// <param name="device">The graphics device used to create GPU resources.</param>
    /// <param name="presenter">The presenter used for frame presentation.</param>
    internal VulkanRenderer(GraphicsDevice device, Presenter presenter)
    {
        _device = device;
        _presenter = presenter;

        var basePath = AppContext.BaseDirectory;
        var vertPath = Path.Combine(basePath, "Assets", "Shaders", "triangle.vert");
        var fragPath = Path.Combine(basePath, "Assets", "Shaders", "triangle.frag");

        var vertSource = File.ReadAllText(vertPath);
        var fragSource = File.ReadAllText(fragPath);

        var vertBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Vertex, vertSource, name: "triangle.vert");
        var fragBytecode = ShaderCompiler.CompileGlsl(ShaderStage.Fragment, fragSource, name: "triangle.frag");

        _vertexShader = _device.CreateShader(new ShaderDescription(
            ShaderStage.Vertex,
            vertBytecode,
            Name: "triangle.vert"));
        _fragmentShader = _device.CreateShader(new ShaderDescription(
            ShaderStage.Fragment,
            fragBytecode,
            Name: "triangle.frag"));
        _pipeline = _device.CreateGraphicsPipeline(new GraphicsPipelineDescription(
            new[] { _vertexShader, _fragmentShader },
            VertexInputDescription.Empty,
            ColorAttachmentFormat: _presenter.ColorFormat));
    }

    /// <summary>
    /// Records and submits a draw command for the current frame, then presents the rendered image.
    /// </summary>
    /// <param name="alpha">The interpolation factor since the last fixed update.</param>
    public void DrawFrame(double alpha)
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
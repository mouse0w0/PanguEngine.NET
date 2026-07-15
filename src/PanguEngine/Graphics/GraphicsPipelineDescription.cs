namespace PanguEngine.Graphics;

/// <summary>
/// Describes a graphics pipeline resource to create.
/// </summary>
public readonly record struct GraphicsPipelineDescription
{
    public GraphicsPipelineDescription()
    {
    }

    /// <summary>
    /// The shaders used by the pipeline.
    /// </summary>
    public required Shader[] Shaders { get; init; }

    /// <summary>
    /// The vertex input layout.
    /// </summary>
    public required VertexInputDescription VertexInput { get; init; }

    /// <summary>
    /// The color attachment formats.
    /// </summary>
    public required TextureFormat[] ColorAttachmentFormats { get; init; }

    /// <summary>
    /// The descriptor set layouts used by the pipeline.
    /// </summary>
    public required DescriptorSetLayout[] DescriptorSetLayouts { get; init; }

    /// <summary>
    /// The primitive topology.
    /// </summary>
    public PrimitiveTopology Topology { get; init; } = PrimitiveTopology.TriangleList;

    /// <summary>
    /// The rasterization state.
    /// </summary>
    public RasterizerDescription Rasterizer { get; init; } = default;

    /// <summary>
    /// The color blend state.
    /// </summary>
    public ColorBlendDescription ColorBlend { get; init; } = default;

    /// <summary>
    /// Whether viewport state is dynamic.
    /// </summary>
    public bool DynamicViewport { get; init; } = true;

    /// <summary>
    /// Whether scissor state is dynamic.
    /// </summary>
    public bool DynamicScissor { get; init; } = true;

    /// <summary>
    /// The depth and stencil state.
    /// </summary>
    public DepthStencilDescription DepthStencil { get; init; } = default;

    /// <summary>
    /// The depth/stencil attachment format, or <see cref="TextureFormat.Undefined" /> when the pipeline does not use one.
    /// </summary>
    public TextureFormat DepthStencilAttachmentFormat { get; init; } = TextureFormat.Undefined;
}
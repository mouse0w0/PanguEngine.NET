namespace PanguEngine.Graphics;

/// <summary>
/// Describes a graphics pipeline resource to create.
/// </summary>
/// <param name="Shaders">The shaders used by the pipeline.</param>
/// <param name="VertexInput">The vertex input layout.</param>
/// <param name="Topology">The primitive topology.</param>
/// <param name="Rasterizer">The rasterization state.</param>
/// <param name="ColorBlend">The color blend state.</param>
/// <param name="ColorAttachmentFormats">The color attachment formats.</param>
/// <param name="DynamicViewport">Whether viewport state is dynamic.</param>
/// <param name="DynamicScissor">Whether scissor state is dynamic.</param>
/// <param name="DescriptorSetLayouts">The descriptor set layouts used by the pipeline.</param>
/// <param name="DepthStencil">The depth and stencil state.</param>
/// <param name="DepthStencilAttachmentFormat">The depth/stencil attachment format, or <see cref="TextureFormat.Undefined" /> when the pipeline does not use one.</param>
public readonly record struct GraphicsPipelineDescription(
    Shader[] Shaders,
    VertexInputDescription VertexInput,
    TextureFormat[] ColorAttachmentFormats,
    DescriptorSetLayout[] DescriptorSetLayouts,
    PrimitiveTopology Topology = PrimitiveTopology.TriangleList,
    RasterizerDescription Rasterizer = default,
    ColorBlendDescription ColorBlend = default,
    bool DynamicViewport = true,
    bool DynamicScissor = true,
    DepthStencilDescription DepthStencil = default,
    TextureFormat DepthStencilAttachmentFormat = TextureFormat.Undefined);
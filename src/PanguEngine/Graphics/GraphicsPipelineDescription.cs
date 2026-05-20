namespace PanguEngine.Graphics;

/// <summary>
/// Describes a graphics pipeline resource to create.
/// </summary>
/// <param name="Shaders">The shaders used by the pipeline.</param>
/// <param name="VertexInput">The vertex input layout.</param>
/// <param name="Topology">The primitive topology.</param>
/// <param name="Rasterizer">The rasterization state.</param>
/// <param name="ColorBlend">The color blend state.</param>
/// <param name="ColorAttachmentFormat">The color attachment format.</param>
/// <param name="DynamicViewport">Whether viewport state is dynamic.</param>
/// <param name="DynamicScissor">Whether scissor state is dynamic.</param>
public readonly record struct GraphicsPipelineDescription(
    ReadOnlyMemory<Shader> Shaders,
    VertexInputDescription VertexInput,
    PrimitiveTopology Topology = PrimitiveTopology.TriangleList,
    RasterizerDescription Rasterizer = default,
    ColorBlendDescription ColorBlend = default,
    TextureFormat ColorAttachmentFormat = TextureFormat.B8G8R8A8Unorm,
    bool DynamicViewport = true,
    bool DynamicScissor = true);
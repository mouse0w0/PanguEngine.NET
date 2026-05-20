namespace PanguEngine.Graphics;

/// <summary>
/// Describes one vertex buffer binding layout.
/// </summary>
/// <param name="Binding">The vertex buffer binding index.</param>
/// <param name="Stride">The byte stride between elements.</param>
/// <param name="InputRate">The input advancement rate.</param>
public readonly record struct VertexBufferLayoutDescription(
    uint Binding,
    uint Stride,
    VertexInputRate InputRate = VertexInputRate.Vertex);
namespace PanguEngine.Graphics;

/// <summary>
/// Describes one vertex attribute.
/// </summary>
/// <param name="Location">The shader input location.</param>
/// <param name="Binding">The vertex buffer binding index.</param>
/// <param name="Format">The attribute format.</param>
/// <param name="Offset">The byte offset within the vertex element.</param>
public readonly record struct VertexAttributeDescription(
    uint Location,
    uint Binding,
    VertexAttributeFormat Format,
    uint Offset);
using System.Diagnostics.CodeAnalysis;

namespace PanguEngine.Graphics;

/// <summary>
/// Describes one vertex attribute.
/// </summary>
public readonly record struct VertexAttributeDescription
{
    /// <summary>
    /// Creates a vertex attribute description.
    /// </summary>
    /// <param name="location">The shader input location.</param>
    /// <param name="binding">The vertex buffer binding index.</param>
    /// <param name="format">The attribute format.</param>
    /// <param name="offset">The byte offset within the vertex element.</param>
    [SetsRequiredMembers]
    public VertexAttributeDescription(uint location, uint binding, VertexAttributeFormat format, uint offset)
    {
        Location = location;
        Binding = binding;
        Format = format;
        Offset = offset;
    }

    /// <summary>
    /// The shader input location.
    /// </summary>
    public required uint Location { get; init; }

    /// <summary>
    /// The vertex buffer binding index.
    /// </summary>
    public required uint Binding { get; init; }

    /// <summary>
    /// The attribute format.
    /// </summary>
    public required VertexAttributeFormat Format { get; init; }

    /// <summary>
    /// The byte offset within the vertex element.
    /// </summary>
    public required uint Offset { get; init; }
}
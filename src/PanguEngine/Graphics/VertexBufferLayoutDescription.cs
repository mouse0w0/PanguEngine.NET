using System.Diagnostics.CodeAnalysis;

namespace PanguEngine.Graphics;

/// <summary>
/// Describes one vertex buffer binding layout.
/// </summary>
public readonly record struct VertexBufferLayoutDescription
{
    public VertexBufferLayoutDescription()
    {
    }

    /// <summary>
    /// Creates a vertex buffer layout description.
    /// </summary>
    /// <param name="binding">The vertex buffer binding index.</param>
    /// <param name="stride">The byte stride between elements.</param>
    /// <param name="inputRate">The input advancement rate.</param>
    [SetsRequiredMembers]
    public VertexBufferLayoutDescription(
        uint binding,
        uint stride,
        VertexInputRate inputRate = VertexInputRate.Vertex)
    {
        Binding = binding;
        Stride = stride;
        InputRate = inputRate;
    }

    /// <summary>
    /// The vertex buffer binding index.
    /// </summary>
    public required uint Binding { get; init; }

    /// <summary>
    /// The byte stride between elements.
    /// </summary>
    public required uint Stride { get; init; }

    /// <summary>
    /// The input advancement rate.
    /// </summary>
    public VertexInputRate InputRate { get; init; } = VertexInputRate.Vertex;
}
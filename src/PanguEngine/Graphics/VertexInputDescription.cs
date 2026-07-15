using System.Diagnostics.CodeAnalysis;

namespace PanguEngine.Graphics;

/// <summary>
/// Describes vertex buffer layouts and vertex attributes for a graphics pipeline.
/// </summary>
public readonly record struct VertexInputDescription
{
    /// <summary>
    /// Creates a vertex input description.
    /// </summary>
    /// <param name="buffers">The vertex buffer layouts.</param>
    /// <param name="attributes">The vertex attribute descriptions.</param>
    [SetsRequiredMembers]
    public VertexInputDescription(
        VertexBufferLayoutDescription[] buffers,
        VertexAttributeDescription[] attributes)
    {
        Buffers = buffers;
        Attributes = attributes;
    }

    /// <summary>
    /// The vertex buffer layouts.
    /// </summary>
    public required VertexBufferLayoutDescription[] Buffers { get; init; }

    /// <summary>
    /// The vertex attribute descriptions.
    /// </summary>
    public required VertexAttributeDescription[] Attributes { get; init; }

    /// <summary>
    /// Gets an empty vertex input description.
    /// </summary>
    public static VertexInputDescription Empty => new([], []);
}
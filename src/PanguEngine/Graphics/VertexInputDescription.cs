namespace PanguEngine.Graphics;

/// <summary>
/// Describes vertex buffer layouts and vertex attributes for a graphics pipeline.
/// </summary>
/// <param name="Buffers">The vertex buffer layouts.</param>
/// <param name="Attributes">The vertex attribute descriptions.</param>
public readonly record struct VertexInputDescription(
    VertexBufferLayoutDescription[] Buffers,
    VertexAttributeDescription[] Attributes)
{
    /// <summary>
    /// Gets an empty vertex input description.
    /// </summary>
    public static VertexInputDescription Empty => new([], []);
}
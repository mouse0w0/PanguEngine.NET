namespace PanguEngine.Graphics;

/// <summary>
/// Flags describing the usage of a buffer.
/// </summary>
[Flags]
public enum BufferUsage
{
    /// <summary>
    /// No usage flags.
    /// </summary>
    None = 0,

    /// <summary>
    /// Buffer can be used as a source for GPU transfer operations.
    /// </summary>
    TransferSource = 1,

    /// <summary>
    /// Buffer can be used as a destination for GPU transfer operations.
    /// </summary>
    TransferDestination = 2,

    /// <summary>
    /// Buffer can be used as a uniform buffer.
    /// </summary>
    Uniform = 4,

    /// <summary>
    /// Buffer can be used as a vertex buffer.
    /// </summary>
    Vertex = 8,

    /// <summary>
    /// Buffer can be used as an index buffer.
    /// </summary>
    Index = 16
}
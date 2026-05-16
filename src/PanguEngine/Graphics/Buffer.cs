namespace PanguEngine.Graphics;

/// <summary>
/// A buffer resource in GPU memory.
/// </summary>
public abstract class Buffer : GraphicsResource
{
    /// <summary>
    /// Gets the size of the buffer in bytes.
    /// </summary>
    public abstract ulong Size { get; }
}
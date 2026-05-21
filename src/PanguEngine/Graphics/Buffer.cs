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

    /// <summary>
    /// Writes a single unmanaged value into the buffer.
    /// </summary>
    /// <typeparam name="T">The unmanaged value type.</typeparam>
    /// <param name="value">The value to write.</param>
    /// <param name="destinationOffset">The destination byte offset within the buffer.</param>
    public abstract void Write<T>(in T value, ulong destinationOffset = 0) where T : unmanaged;

    /// <summary>
    /// Writes unmanaged data into the buffer.
    /// </summary>
    /// <typeparam name="T">The unmanaged data element type.</typeparam>
    /// <param name="data">The data to write.</param>
    /// <param name="destinationOffset">The destination byte offset within the buffer.</param>
    public abstract void Write<T>(ReadOnlySpan<T> data, ulong destinationOffset = 0) where T : unmanaged;
}
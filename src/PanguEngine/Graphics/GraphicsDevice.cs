namespace PanguEngine.Graphics;

/// <summary>
/// Represents a logical graphics device.
/// </summary>
public abstract class GraphicsDevice
{
    /// <summary>
    /// Creates a buffer with the given description.
    /// </summary>
    /// <param name="description">The buffer description.</param>
    /// <returns>The created buffer.</returns>
    public abstract Buffer CreateBuffer(in BufferDescription description);

    public abstract GraphicsUploadHandle UploadBuffer<T>(
        Buffer destination,
        ReadOnlySpan<T> data,
        ulong destinationOffset = 0) where T : unmanaged;
}
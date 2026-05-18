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

    /// <summary>
    /// Queues data for upload into a buffer.
    /// </summary>
    /// <typeparam name="T">The unmanaged type of the data elements.</typeparam>
    /// <param name="destination">The destination buffer.</param>
    /// <param name="data">The data to upload.</param>
    /// <param name="destinationOffset">The destination byte offset within the buffer.</param>
    /// <returns>A handle that represents the queued upload completion state.</returns>
    public abstract GraphicsUploadHandle UploadBuffer<T>(
        Buffer destination,
        ReadOnlySpan<T> data,
        ulong destinationOffset = 0) where T : unmanaged;

    /// <summary>
    /// Creates a two-dimensional texture with the given description.
    /// </summary>
    /// <param name="description">The texture description.</param>
    /// <returns>The created texture.</returns>
    public abstract Texture2D CreateTexture2D(in Texture2DDescription description);

    /// <summary>
    /// Queues data for upload into a two-dimensional texture.
    /// </summary>
    /// <param name="destination">The destination texture.</param>
    /// <param name="data">The texture data to upload.</param>
    /// <returns>A handle that represents the queued upload completion state.</returns>
    public abstract GraphicsUploadHandle UploadTexture2D(
        Texture2D destination,
        ReadOnlySpan<byte> data);
}
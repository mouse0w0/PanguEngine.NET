namespace PanguEngine.Graphics;

/// <summary>
/// Represents a presentation target and frame boundary provider.
/// </summary>
public abstract class Presenter : GraphicsResource
{
    /// <summary>
    /// Gets the current presentation width.
    /// </summary>
    public abstract uint Width { get; }

    /// <summary>
    /// Gets the current presentation height.
    /// </summary>
    public abstract uint Height { get; }

    /// <summary>
    /// Gets the current presentation color format.
    /// </summary>
    public abstract TextureFormat ColorFormat { get; }

    /// <summary>
    /// Begins a graphics frame for command recording.
    /// </summary>
    /// <returns>The active graphics frame.</returns>
    public abstract Frame BeginFrame();

    /// <summary>
    /// Ends, submits, and presents a graphics frame.
    /// </summary>
    /// <param name="frame">The frame returned by <see cref="BeginFrame"/>.</param>
    public abstract void EndFrame(Frame frame);
}
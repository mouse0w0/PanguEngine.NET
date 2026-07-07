using System.Diagnostics.CodeAnalysis;

namespace PanguEngine.Graphics;

/// <summary>
/// Represents a presentation target and frame boundary provider.
/// </summary>
public abstract class Presenter
{
    /// <summary>
    /// Gets whether the presenter has been destroyed.
    /// </summary>
    public abstract bool IsDestroyed { get; }

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
    /// Gets the number of in-flight frame slots available to frame-local resources.
    /// </summary>
    public abstract uint MaxFramesInFlight { get; }

    /// <summary>
    /// Begins a graphics frame for command recording.
    /// </summary>
    /// <returns>The active graphics frame.</returns>
    public abstract Frame BeginFrame();

    /// <summary>
    /// Attempts to begin a graphics frame for command recording.
    /// </summary>
    /// <param name="frame">The active graphics frame when one is available.</param>
    /// <returns>Whether a frame was available for command recording.</returns>
    public abstract bool TryBeginFrame([MaybeNullWhen(false)] out Frame frame);

    /// <summary>
    /// Ends, submits, and presents a graphics frame.
    /// </summary>
    /// <param name="frame">The frame returned by <see cref="BeginFrame"/>.</param>
    public abstract void EndFrame(Frame frame);

    /// <summary>
    /// Destroys the presenter.
    /// </summary>
    internal abstract void Destroy();
}
namespace PanguEngine.Graphics;

/// <summary>
/// Represents the backend-independent context for the active graphics frame.
/// </summary>
public abstract class Frame
{
    /// <summary>
    /// Gets the monotonically increasing number assigned to this frame.
    /// </summary>
    public abstract ulong FrameNumber { get; }

    /// <summary>
    /// Gets the in-flight frame slot assigned to this frame.
    /// </summary>
    public abstract uint FrameSlot { get; }

    /// <summary>
    /// Gets the command list for this frame.
    /// </summary>
    public abstract CommandList CommandList { get; }

    /// <summary>
    /// Gets the width of the active frame target.
    /// </summary>
    public abstract uint Width { get; }

    /// <summary>
    /// Gets the height of the active frame target.
    /// </summary>
    public abstract uint Height { get; }

    /// <summary>
    /// Gets the color output texture view for the active frame.
    /// </summary>
    public abstract TextureView ColorOutput { get; }
}
namespace PanguEngine.Client.UI;

/// <summary>
/// Represents an immutable backend-independent UI drawing command.
/// </summary>
public abstract class UiDrawCommand
{
    private protected UiDrawCommand(Rect? clip, double opacity)
    {
        Clip = clip;
        Opacity = opacity;
    }

    /// <summary>
    /// Gets the final explicit clip in screen logical coordinates, or null when no UI clip applies.
    /// </summary>
    public Rect? Clip { get; }

    /// <summary>
    /// Gets the accumulated opacity applied in addition to the command color alpha.
    /// </summary>
    public double Opacity { get; }
}

/// <summary>
/// Represents a solid-color rectangle in screen logical coordinates.
/// </summary>
public sealed class UiFillRectangleCommand : UiDrawCommand
{
    internal UiFillRectangleCommand(
        Rect bounds,
        Color color,
        Rect? clip,
        double opacity)
        : base(clip, opacity)
    {
        Bounds = bounds;
        Color = color;
    }

    /// <summary>
    /// Gets the unclipped rectangle bounds in screen logical coordinates.
    /// </summary>
    public Rect Bounds { get; }

    /// <summary>
    /// Gets the non-premultiplied fill color.
    /// </summary>
    public Color Color { get; }
}

/// <summary>
/// Represents an image draw in screen logical coordinates.
/// </summary>
public sealed class UiDrawImageCommand : UiDrawCommand
{
    internal UiDrawImageCommand(
        Rect bounds,
        UiImage image,
        Rect sourceRect,
        ImageSamplingMode samplingMode,
        Rect? clip,
        double opacity)
        : base(clip, opacity)
    {
        Bounds = bounds;
        Image = image;
        SourceRect = sourceRect;
        SamplingMode = samplingMode;
    }

    /// <summary>
    /// Gets the unclipped destination bounds in screen logical coordinates.
    /// </summary>
    public Rect Bounds { get; }

    /// <summary>
    /// Gets the image source retained by this immutable command.
    /// </summary>
    public UiImage Image { get; }

    /// <summary>
    /// Gets the source region in image pixel coordinates.
    /// </summary>
    public Rect SourceRect { get; }

    /// <summary>
    /// Gets the image sampling mode.
    /// </summary>
    public ImageSamplingMode SamplingMode { get; }
}

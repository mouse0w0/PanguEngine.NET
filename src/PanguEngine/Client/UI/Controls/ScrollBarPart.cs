using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Client.UI.Controls;

/// <summary>
/// Identifies the interactive role of a scroll bar part.
/// </summary>
internal enum ScrollBarPartKind
{
    /// <summary>The track between the two arrow buttons.</summary>
    Track,

    /// <summary>The draggable thumb.</summary>
    Thumb,

    /// <summary>The arrow that decreases the value.</summary>
    Decrease,

    /// <summary>The arrow that increases the value.</summary>
    Increase
}

/// <summary>
/// Provides one hit-testable interactive part of a <see cref="ScrollBar"/>.
/// </summary>
/// <remarks>
/// The two arrow parts host a single non-hit-testable <see cref="Path"/> glyph. The glyph data, fill, and
/// direction are supplied by descendant style rules keyed on the owning scroll bar's orientation pseudo class.
/// </remarks>
internal sealed class ScrollBarPart : Control
{
    private const double DefaultArrowSize = 8;

    private readonly Path? _arrow;

    static ScrollBarPart()
    {
        UiCssRegistry.RegisterElement<ScrollBarPart>("ScrollBarPart");
    }

    internal ScrollBarPart(ScrollBarPartKind kind)
    {
        Kind = kind;
        Classes.Add(GetRoleClassName(kind));
        if (kind is not (ScrollBarPartKind.Decrease or ScrollBarPartKind.Increase))
            return;

        var arrow = new Path { IsHitTestVisible = false };
        arrow.Classes.Add("scrollbar-arrow");
        Children.Add(arrow);
        _arrow = arrow;
    }

    /// <summary>
    /// Gets the interactive role of this part.
    /// </summary>
    internal ScrollBarPartKind Kind { get; }

    /// <inheritdoc />
    protected override Size MeasureContent(Size availableSize)
    {
        if (_arrow is null)
            return Size.Zero;

        _arrow.Measure(availableSize);
        return _arrow.DesiredSize;
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Rect contentBounds)
    {
        if (_arrow is null)
            return;

        var size = Math.Min(DefaultArrowSize, Math.Min(contentBounds.Width, contentBounds.Height));
        if (size <= 0)
        {
            _arrow.Arrange(Rect.Zero);
            return;
        }

        _arrow.Arrange(new Rect(
            contentBounds.X + (contentBounds.Width - size) / 2,
            contentBounds.Y + (contentBounds.Height - size) / 2,
            size,
            size));
    }

    private static string GetRoleClassName(ScrollBarPartKind kind) => kind switch
    {
        ScrollBarPartKind.Track => "scrollbar-track",
        ScrollBarPartKind.Thumb => "scrollbar-thumb",
        ScrollBarPartKind.Decrease => "scrollbar-decrease",
        _ => "scrollbar-increase"
    };
}

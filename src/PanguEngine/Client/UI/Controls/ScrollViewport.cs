using PanguEngine.Collections;

namespace PanguEngine.Client.UI.Controls;

/// <summary>
/// Hosts the single content node of a scroll view inside a clipped viewport.
/// </summary>
/// <remarks>
/// The viewport arranges its content at the negated scroll offset so that drawing and hit testing
/// share one geometry. It never measures its content with an infinite constraint of its own; the
/// owning scroll view supplies the per-axis constraint through <see cref="ContentMeasureConstraint"/>.
/// Content migration is detected through the observable child collection, so a node moved to another
/// parent clears the owning scroll view's content reference without a parent-level hook.
/// </remarks>
internal sealed class ScrollViewport : Parent
{
    private UiNode? _content;

    internal ScrollViewport()
    {
        ClipToBounds = true;
        Children.Changed += OnChildrenChanged;
    }

    /// <summary>
    /// Gets or sets the owning scroll view notified when content leaves this viewport.
    /// </summary>
    internal ScrollView? Owner { get; init; }

    /// <summary>
    /// Gets or sets the constraint applied when measuring the content node.
    /// </summary>
    internal Size ContentMeasureConstraint { get; set; } = Size.Infinite;

    /// <summary>
    /// Gets or sets the effective content offset used when arranging the content node.
    /// </summary>
    internal Point Offset { get; set; }

    /// <summary>
    /// Replaces the content node, moving nodes through the validated parent operations.
    /// </summary>
    /// <param name="content">The new content node, or null to clear the content.</param>
    internal void SetContent(UiNode? content)
    {
        if (ReferenceEquals(_content, content))
            return;

        if (_content is not null && ReferenceEquals(_content.Parent, this))
            Children.Remove(_content);

        _content = content;
        if (content is not null && !ReferenceEquals(content.Parent, this))
            Children.Add(content);
    }

    /// <inheritdoc />
    protected override Size MeasureCore(Size availableSize)
    {
        var content = _content;
        if (content is null)
            return Size.Zero;

        content.Measure(ContentMeasureConstraint);
        return content.DesiredSize;
    }

    /// <inheritdoc />
    protected override void ArrangeCore(Size finalSize)
    {
        var content = _content;
        if (content is null)
            return;

        var extent = content.DesiredSize;
        var slotWidth = Math.Max(finalSize.Width, extent.Width);
        var slotHeight = Math.Max(finalSize.Height, extent.Height);
        content.Arrange(new Rect(-Offset.X, -Offset.Y, slotWidth, slotHeight));
    }

    private void OnChildrenChanged(object? sender, ListChangedEventArgs<UiNode> change)
    {
        foreach (var child in change.OldItems)
        {
            if (ReferenceEquals(child, _content) && !ReferenceEquals(child.Parent, this))
            {
                _content = null;
                Owner?.OnViewportContentRemoved(child);
            }
        }
    }
}

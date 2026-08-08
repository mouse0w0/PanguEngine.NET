namespace PanguEngine.Client.UI;

public abstract partial class UiNode
{
    /// <summary>
    /// Identifies the <see cref="Opacity"/> property.
    /// </summary>
    public static readonly UiProperty<double> OpacityProperty =
        UiProperty.Register<UiNode, double>(
            nameof(Opacity),
            1,
            UiPropertyInvalidation.Render);

    /// <summary>
    /// Gets or sets the opacity applied to this node and its descendants.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when this property is modified while the owning screen is generating drawing commands.
    /// </exception>
    public double Opacity
    {
        get => GetValue(OpacityProperty);
        set => SetValue(OpacityProperty, value);
    }

    /// <summary>
    /// Draws this node using local logical coordinates.
    /// </summary>
    /// <param name="context">The constrained drawing context for this node.</param>
    protected virtual void DrawCore(UiDrawingContext context)
    {
    }

    internal void AppendDrawCommands(
        List<UiDrawCommand> commands,
        UiDrawingState inheritedState)
    {
        if (!IsArrangeValid ||
            Visibility != Visibility.Visible ||
            inheritedState.IsClipEmpty ||
            inheritedState.Opacity == 0)
        {
            return;
        }

        var opacity = Opacity;
        if (!double.IsFinite(opacity) || opacity < 0 || opacity > 1)
            throw new InvalidOperationException("Opacity must be finite and between zero and one.");

        var combinedOpacity = inheritedState.Opacity * opacity;
        if (combinedOpacity == 0)
            return;

        var originX = UiDrawingContext.AddCoordinate(
            inheritedState.OriginX,
            LayoutBounds.X);
        var originY = UiDrawingContext.AddCoordinate(
            inheritedState.OriginY,
            LayoutBounds.Y);
        var nodeState = inheritedState with
        {
            OriginX = originX,
            OriginY = originY,
            Opacity = combinedOpacity
        };

        var context = new UiDrawingContext(commands, nodeState);
        try
        {
            DrawCore(context);
            context.Complete();
        }
        catch
        {
            context.Abort();
            throw;
        }

        if (this is not Parent parent)
            return;

        var childState = nodeState;
        if (parent.ClipToBounds)
        {
            childState = UiDrawingContext.ApplyClip(
                childState,
                new Rect(
                    originX,
                    originY,
                    LayoutBounds.Width,
                    LayoutBounds.Height));
            if (childState.IsClipEmpty)
                return;
        }

        foreach (var child in parent.Children)
            child.AppendDrawCommands(commands, childState);
    }
}

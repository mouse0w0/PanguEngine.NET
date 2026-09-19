using PanguEngine.Client.UI.Drawing;

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
        double inheritedOpacity)
    {
        if (!IsArrangeValid ||
            Visibility != Visibility.Visible ||
            inheritedOpacity == 0)
        {
            return;
        }

        var opacity = Opacity;
        if (!double.IsFinite(opacity) || opacity < 0 || opacity > 1)
            throw new InvalidOperationException("Opacity must be finite and between zero and one.");

        var combinedOpacity = inheritedOpacity * opacity;
        if (combinedOpacity == 0)
            return;

        var transformIndex = UiDrawingContext.PushTransform(
            commands, new Point(LayoutBounds.X, LayoutBounds.Y), 1);
        var opacityIndex = UiDrawingContext.PushOpacity(commands, opacity);
        var context = new UiDrawingContext(commands);
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

        if (this is Parent parent)
        {
            var clipIndex = parent.ClipToBounds
                ? UiDrawingContext.PushCommand(commands, new UiPushClipCommand(
                    new Rect(0, 0, LayoutBounds.Width, LayoutBounds.Height)))
                : -1;
            foreach (var child in parent.Children)
                child.AppendDrawCommands(commands, combinedOpacity);
            UiDrawingContext.PopCommand(commands, clipIndex);
        }

        UiDrawingContext.PopCommand(commands, opacityIndex);
        UiDrawingContext.PopCommand(commands, transformIndex);
    }
}

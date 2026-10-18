using PanguEngine.ComponentModel;
using PanguEngine.UI.Drawing;

namespace PanguEngine.UI;

public abstract partial class Node
{
    /// <summary>
    /// Identifies the <see cref="Opacity"/> property.
    /// </summary>
    public static readonly Property<double> OpacityProperty =
        Property.Register<Node, double>(
            nameof(Opacity),
            1,
            validate: static value => IsFiniteNonNegative(value) && value <= 1,
            validationMessage: "Opacity must be finite and between zero and one.");

    /// <summary>
    /// Gets or sets the opacity applied to this node and its descendants.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is not finite or is outside the range from zero to one.</exception>
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
    protected virtual void DrawCore(DrawingContext context)
    {
    }

    internal void AppendDrawCommands(
        List<DrawCommand> commands,
        double inheritedOpacity)
    {
        if (!IsArrangeValid ||
            Visibility != Visibility.Visible ||
            inheritedOpacity == 0)
        {
            return;
        }

        var opacity = Opacity;
        var combinedOpacity = inheritedOpacity * opacity;
        if (combinedOpacity == 0)
            return;

        var transformIndex = DrawingContext.PushTransform(
            commands, new Point(LayoutBounds.X, LayoutBounds.Y), 1);
        var opacityIndex = DrawingContext.PushOpacity(commands, opacity);
        var context = new DrawingContext(commands);
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
                ? DrawingContext.PushCommand(commands, parent.CreateDescendantClip())
                : -1;
            foreach (var child in parent.ReadOnlyChildren)
                child.AppendDrawCommands(commands, combinedOpacity);
            DrawingContext.PopCommand(commands, clipIndex);
        }

        DrawingContext.PopCommand(commands, opacityIndex);
        DrawingContext.PopCommand(commands, transformIndex);
    }
}

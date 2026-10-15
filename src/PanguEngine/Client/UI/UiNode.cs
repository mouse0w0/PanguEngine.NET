using PanguEngine.Client.UI.Drawing;
using PanguEngine.ComponentModel;
using PanguEngine.Collections;
using PanguEngine.Graphics.Text;

namespace PanguEngine.Client.UI;

/// <summary>
/// Provides the base property host for retained-mode UI nodes.
/// </summary>
/// <remarks>
/// Properties cannot be modified while the owning screen is generating drawing commands.
/// </remarks>
public abstract partial class UiNode : ObservableObject
{
    private static readonly PropertyKey<Parent?> ParentPropertyKey =
        Property.RegisterReadOnly<UiNode, Parent?>(
            nameof(Parent),
            onChanged: static (node, oldParent, _) => node.OnInheritanceParentChanged(oldParent));

    private static readonly PropertyKey<UiScreen?> ScreenPropertyKey =
        Property.RegisterReadOnly<UiNode, UiScreen?>(nameof(Screen));

    /// <summary>
    /// Identifies the <see cref="Parent"/> property.
    /// </summary>
    public static readonly Property<Parent?> ParentProperty = ParentPropertyKey.Property;

    /// <summary>
    /// Identifies the <see cref="Screen"/> property.
    /// </summary>
    public static readonly Property<UiScreen?> ScreenProperty = ScreenPropertyKey.Property;

    /// <summary>Identifies the inherited <see cref="FontFamily"/> property.</summary>
    public static readonly Property<string> FontFamilyProperty =
        Property.Register<UiNode, string>(
            nameof(FontFamily), string.Empty,
            onChanged: static (node, _, _) => node.InvalidateMeasure(),
            validate: static value => value is not null,
            validationMessage: "FontFamily cannot be null.",
            inherits: true);

    /// <summary>Identifies the inherited <see cref="FontWeight"/> property.</summary>
    public static readonly Property<FontWeight> FontWeightProperty =
        Property.Register<UiNode, FontWeight>(
            nameof(FontWeight), FontWeight.Normal,
            onChanged: static (node, _, _) => node.InvalidateMeasure(),
            inherits: true);

    /// <summary>Identifies the inherited <see cref="FontStyle"/> property.</summary>
    public static readonly Property<FontStyle> FontStyleProperty =
        Property.Register<UiNode, FontStyle>(
            nameof(FontStyle), FontStyle.Normal,
            onChanged: static (node, _, _) => node.InvalidateMeasure(),
            inherits: true);

    /// <summary>Identifies the inherited <see cref="FontSize"/> property.</summary>
    public static readonly Property<double> FontSizeProperty =
        Property.Register<UiNode, double>(
            nameof(FontSize), 16d,
            onChanged: static (node, _, _) => node.InvalidateMeasure(),
            validate: IsFinitePositive,
            validationMessage: "FontSize must be positive and finite.",
            inherits: true);

    /// <summary>Identifies the inherited <see cref="LineHeight"/> property.</summary>
    public static readonly Property<double> LineHeightProperty =
        Property.Register<UiNode, double>(
            nameof(LineHeight), 1d,
            onChanged: static (node, _, _) => node.InvalidateMeasure(),
            validate: IsFinitePositive,
            validationMessage: "LineHeight must be positive and finite.",
            inherits: true);

    /// <summary>Identifies the inherited <see cref="Color"/> property.</summary>
    public static readonly Property<Color> ColorProperty =
        Property.Register<UiNode, Color>(
            nameof(Color), new Color(255, 255, 255),
            inherits: true);

    /// <summary>
    /// Initializes a UI node.
    /// </summary>
    protected UiNode()
    {
        Classes = new ObservableSet<string>(StringComparer.Ordinal);
        Classes.Changed += (_, _) => InvalidateStyle();
    }

    /// <summary>
    /// Gets the framework-maintained direct parent of this node.
    /// </summary>
    /// <remarks>
    /// The value is null when this node has no parent. Property change notifications occur after
    /// this value changes, before any resulting screen ownership change.
    /// </remarks>
    public Parent? Parent => GetValue(ParentProperty);

    /// <summary>
    /// Gets the UI screen that owns this node.
    /// </summary>
    /// <remarks>
    /// The value is null when this node has no owning screen. Property change notifications occur
    /// after this node's value changes and before its descendants are updated.
    /// </remarks>
    public UiScreen? Screen => GetValue(ScreenProperty);

    /// <summary>
    /// Gets or sets the preferred font family, inherited from the parent when no local or styled value applies.
    /// </summary>
    /// <remarks>An empty family name requests the engine default font.</remarks>
    /// <exception cref="ArgumentException">Thrown when the value is null.</exception>
    public string FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    /// <summary>
    /// Gets or sets the preferred font weight, inherited from the parent when no local or styled value applies.
    /// </summary>
    public FontWeight FontWeight
    {
        get => GetValue(FontWeightProperty);
        set => SetValue(FontWeightProperty, value);
    }

    /// <summary>
    /// Gets or sets the preferred font style, inherited from the parent when no local or styled value applies.
    /// </summary>
    public FontStyle FontStyle
    {
        get => GetValue(FontStyleProperty);
        set => SetValue(FontStyleProperty, value);
    }

    /// <summary>
    /// Gets or sets the text size in logical pixels, inherited from the parent when no local or styled value applies.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is not finite and positive.</exception>
    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the natural line height multiplier, inherited from the parent when no local or styled value applies.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is not finite and positive.</exception>
    public double LineHeight
    {
        get => GetValue(LineHeightProperty);
        set => SetValue(LineHeightProperty, value);
    }

    /// <summary>
    /// Gets or sets the non-premultiplied foreground color, inherited from the parent when no local or styled value applies.
    /// </summary>
    public Color Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    /// <inheritdoc />
    protected override ObservableObject? InheritanceParent => Parent;

    /// <summary>
    /// Moves this node to the visual front of its siblings.
    /// </summary>
    /// <remarks>
    /// The node becomes the last child in drawing order. Calling this method without a parent or
    /// while already at the front has no effect.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a tree owned by an open screen is modified from the wrong thread.
    /// </exception>
    public void MoveToFront() =>
        Parent?.MoveChildToFront(this);

    /// <summary>
    /// Moves this node to the visual back of its siblings.
    /// </summary>
    /// <remarks>
    /// The node becomes the first child in drawing order. Calling this method without a parent or
    /// while already at the back has no effect.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a tree owned by an open screen is modified from the wrong thread.
    /// </exception>
    public void MoveToBack() =>
        Parent?.MoveChildToBack(this);

    internal Font CreateFontRequest() => new(FontFamily, FontWeight, FontStyle);

    internal void SetParent(Parent? parent) =>
        SetValue(ParentPropertyKey, parent);

    internal void SetScreenRecursive(UiScreen? screen)
    {
        if (!ReferenceEquals(Screen, screen))
            StopActiveTickers();
        SetValue(ScreenPropertyKey, screen);
        if (this is not Parent parent)
            return;

        foreach (var child in parent.ReadOnlyChildren)
            child.SetScreenRecursive(screen);
    }

    internal void InvalidateTreeStructure()
    {
        for (var node = this; node is not null; node = node.Parent)
            node.OnTreeStructureInvalidated();
    }

    partial void OnTreeStructureInvalidated();
}

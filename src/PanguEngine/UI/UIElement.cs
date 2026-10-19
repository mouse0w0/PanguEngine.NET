using PanguEngine.Collections;
using PanguEngine.ComponentModel;
using PanguEngine.Graphics.Text;
using PanguEngine.UI.Drawing;

namespace PanguEngine.UI;

/// <summary>
/// Provides the base property host for retained-mode UI elements.
/// </summary>
/// <remarks>
/// Properties cannot be modified while the owning scene is generating drawing commands.
/// </remarks>
public abstract partial class UIElement : StyledObject
{
    private static readonly PropertyKey<Container?> ParentPropertyKey =
        Property.RegisterReadOnly<UIElement, Container?>(
            nameof(Parent),
            onChanged: static (node, oldParent, _) => node.OnInheritanceParentChanged(oldParent));

    private static readonly PropertyKey<UIScene?> ScenePropertyKey =
        Property.RegisterReadOnly<UIElement, UIScene?>(nameof(Scene));

    /// <summary>
    /// Identifies the <see cref="Parent"/> property.
    /// </summary>
    public static readonly Property<Container?> ParentProperty = ParentPropertyKey.Property;

    /// <summary>
    /// Identifies the <see cref="Scene"/> property.
    /// </summary>
    public static readonly Property<UIScene?> SceneProperty = ScenePropertyKey.Property;

    /// <summary>Identifies the inherited <see cref="FontFamily"/> property.</summary>
    public static readonly Property<string> FontFamilyProperty =
        Property.Register<UIElement, string>(
            nameof(FontFamily), string.Empty,
            onChanged: static (node, _, _) => node.InvalidateMeasure(),
            validate: static value => value is not null,
            validationMessage: "FontFamily cannot be null.",
            inherits: true);

    /// <summary>Identifies the inherited <see cref="FontWeight"/> property.</summary>
    public static readonly Property<FontWeight> FontWeightProperty =
        Property.Register<UIElement, FontWeight>(
            nameof(FontWeight), FontWeight.Normal,
            onChanged: static (node, _, _) => node.InvalidateMeasure(),
            inherits: true);

    /// <summary>Identifies the inherited <see cref="FontStyle"/> property.</summary>
    public static readonly Property<FontStyle> FontStyleProperty =
        Property.Register<UIElement, FontStyle>(
            nameof(FontStyle), FontStyle.Normal,
            onChanged: static (node, _, _) => node.InvalidateMeasure(),
            inherits: true);

    /// <summary>Identifies the inherited <see cref="FontSize"/> property.</summary>
    public static readonly Property<double> FontSizeProperty =
        Property.Register<UIElement, double>(
            nameof(FontSize), 16d,
            onChanged: static (node, _, _) => node.InvalidateMeasure(),
            validate: IsFinitePositive,
            validationMessage: "FontSize must be positive and finite.",
            inherits: true);

    /// <summary>Identifies the inherited <see cref="LineHeight"/> property.</summary>
    public static readonly Property<double> LineHeightProperty =
        Property.Register<UIElement, double>(
            nameof(LineHeight), 1d,
            onChanged: static (node, _, _) => node.InvalidateMeasure(),
            validate: IsFinitePositive,
            validationMessage: "LineHeight must be positive and finite.",
            inherits: true);

    /// <summary>Identifies the inherited <see cref="Color"/> property.</summary>
    public static readonly Property<Color> ColorProperty =
        Property.Register<UIElement, Color>(
            nameof(Color), new Color(255, 255, 255),
            inherits: true);

    /// <summary>
    /// Initializes a UI element.
    /// </summary>
    protected UIElement()
    {
        Classes = new ObservableSet<string>(StringComparer.Ordinal);
        Classes.Changed += (_, _) => InvalidateStyle();
    }

    /// <summary>
    /// Gets the framework-maintained direct parent of this element.
    /// </summary>
    /// <remarks>
    /// The value is null when this element has no parent. Property change notifications occur after
    /// this value changes, before any resulting scene ownership change.
    /// </remarks>
    public Container? Parent => GetValue(ParentProperty);

    /// <summary>
    /// Gets the UI scene that owns this element.
    /// </summary>
    /// <remarks>
    /// The value is null when this element has no owning scene. Property change notifications occur
    /// after this element's value changes and before its descendants are updated.
    /// </remarks>
    public UIScene? Scene => GetValue(SceneProperty);

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
    protected override StyledObject? InheritanceParent => Parent;

    /// <summary>
    /// Moves this element to the visual front of its siblings.
    /// </summary>
    /// <remarks>
    /// The element becomes the last child in drawing order. Calling this method without a parent or
    /// while already at the front has no effect.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a tree owned by an open scene is modified from the wrong thread.
    /// </exception>
    public void MoveToFront() =>
        Parent?.MoveChildToFront(this);

    /// <summary>
    /// Moves this element to the visual back of its siblings.
    /// </summary>
    /// <remarks>
    /// The element becomes the first child in drawing order. Calling this method without a parent or
    /// while already at the back has no effect.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a tree owned by an open scene is modified from the wrong thread.
    /// </exception>
    public void MoveToBack() =>
        Parent?.MoveChildToBack(this);

    internal Font CreateFontRequest() => new(FontFamily, FontWeight, FontStyle);

    internal void SetParent(Container? parent) =>
        SetValue(ParentPropertyKey, parent);

    internal void SetSceneRecursive(UIScene? scene)
    {
        if (!ReferenceEquals(Scene, scene))
            StopActiveTickers();
        SetValue(ScenePropertyKey, scene);
        if (this is not Container parent)
            return;

        foreach (var child in parent.ReadOnlyChildren)
            child.SetSceneRecursive(scene);
    }

    internal void InvalidateTreeStructure()
    {
        for (var node = this; node is not null; node = node.Parent)
            node.OnTreeStructureInvalidated();
    }

    partial void OnTreeStructureInvalidated();
}
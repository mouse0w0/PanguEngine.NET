using PanguEngine.Graphics.Text;

namespace PanguEngine.Client.UI;

/// <summary>
/// Displays an immutable layout of plain text.
/// </summary>
public sealed class Text : UiNode
{
    /// <summary>
    /// Identifies the <see cref="Content"/> property.
    /// </summary>
    public static readonly UiProperty<string> ContentProperty =
        UiProperty.Register<Text, string>(
            nameof(Content),
            string.Empty,
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);

    /// <summary>
    /// Identifies the <see cref="Font"/> property.
    /// </summary>
    public static readonly UiProperty<Font> FontProperty =
        UiProperty.Register<Text, Font>(
            nameof(Font),
            new Font(string.Empty),
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);

    /// <summary>
    /// Identifies the <see cref="FontSize"/> property.
    /// </summary>
    public static readonly UiProperty<double> FontSizeProperty =
        UiProperty.Register<Text, double>(
            nameof(FontSize),
            16,
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);

    /// <summary>
    /// Identifies the <see cref="Color"/> property.
    /// </summary>
    public static readonly UiProperty<Color> ColorProperty =
        UiProperty.Register<Text, Color>(
            nameof(Color),
            new Color(255, 255, 255),
            UiPropertyInvalidation.Render);

    /// <summary>
    /// Identifies the <see cref="LineHeight"/> property.
    /// </summary>
    public static readonly UiProperty<double> LineHeightProperty =
        UiProperty.Register<Text, double>(
            nameof(LineHeight),
            1,
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);

    /// <summary>
    /// Identifies the <see cref="Wrapping"/> property.
    /// </summary>
    public static readonly UiProperty<TextWrapping> WrappingProperty =
        UiProperty.Register<Text, TextWrapping>(
            nameof(Wrapping),
            TextWrapping.NoWrap,
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);

    /// <summary>
    /// Identifies the <see cref="Alignment"/> property.
    /// </summary>
    public static readonly UiProperty<TextAlignment> AlignmentProperty =
        UiProperty.Register<Text, TextAlignment>(
            nameof(Alignment),
            TextAlignment.Left,
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);

    private TextLayout? _layout;

    /// <summary>
    /// Initializes a text node.
    /// </summary>
    public Text()
    {
    }

    /// <summary>
    /// Gets or sets the plain UTF-16 text content.
    /// </summary>
    public string Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <summary>
    /// Gets or sets the preferred font request.
    /// </summary>
    public Font Font
    {
        get => GetValue(FontProperty);
        set => SetValue(FontProperty, value);
    }

    /// <summary>
    /// Gets or sets the font size in logical pixels.
    /// </summary>
    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the non-premultiplied text color.
    /// </summary>
    public Color Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the natural line height multiplier.
    /// </summary>
    public double LineHeight
    {
        get => GetValue(LineHeightProperty);
        set => SetValue(LineHeightProperty, value);
    }

    /// <summary>
    /// Gets or sets the automatic line wrapping mode.
    /// </summary>
    public TextWrapping Wrapping
    {
        get => GetValue(WrappingProperty);
        set => SetValue(WrappingProperty, value);
    }

    /// <summary>
    /// Gets or sets the horizontal line alignment.
    /// </summary>
    public TextAlignment Alignment
    {
        get => GetValue(AlignmentProperty);
        set => SetValue(AlignmentProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureCore(Size availableSize)
    {
        var layout = TextServices.TextLayoutEngine.Layout(new TextLayoutRequest(
            Content,
            Font,
            FontSize,
            availableSize.Width,
            LineHeight,
            Wrapping,
            Alignment));
        _layout = layout;

        return new Size(layout.Width, layout.Height);
    }

    /// <inheritdoc />
    protected override void DrawCore(UiDrawingContext context) =>
        context.DrawText(Point.Zero, _layout!, FontSize, Color);
}

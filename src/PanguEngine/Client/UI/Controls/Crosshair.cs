using PanguEngine.Client.UI.Drawing;

namespace PanguEngine.Client.UI.Controls;

/// <summary>
/// Draws a configurable crosshair directly into the UI command stream.
/// </summary>
public sealed class Crosshair : UiNode
{
    /// <summary>Identifies the <see cref="Color"/> property.</summary>
    public static readonly UiProperty<Color> ColorProperty =
        UiProperty.Register<Crosshair, Color>(
            nameof(Color),
            new Color(255, 255, 255),
            UiPropertyInvalidation.Render);

    /// <summary>Identifies the <see cref="Length"/> property.</summary>
    public static readonly UiProperty<double> LengthProperty =
        UiProperty.Register<Crosshair, double>(
            nameof(Length),
            8,
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);

    /// <summary>Identifies the <see cref="Thickness"/> property.</summary>
    public static readonly UiProperty<double> ThicknessProperty =
        UiProperty.Register<Crosshair, double>(
            nameof(Thickness),
            2,
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);

    /// <summary>Identifies the <see cref="Gap"/> property.</summary>
    public static readonly UiProperty<double> GapProperty =
        UiProperty.Register<Crosshair, double>(
            nameof(Gap),
            3,
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);

    /// <summary>Identifies the <see cref="OutlineColor"/> property.</summary>
    public static readonly UiProperty<Color> OutlineColorProperty =
        UiProperty.Register<Crosshair, Color>(
            nameof(OutlineColor),
            new Color(0, 0, 0),
            UiPropertyInvalidation.Render);

    /// <summary>Identifies the <see cref="OutlineThickness"/> property.</summary>
    public static readonly UiProperty<double> OutlineThicknessProperty =
        UiProperty.Register<Crosshair, double>(
            nameof(OutlineThickness),
            1,
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);

    /// <summary>Identifies the <see cref="Shape"/> property.</summary>
    public static readonly UiProperty<CrosshairShape> ShapeProperty =
        UiProperty.Register<Crosshair, CrosshairShape>(
            nameof(Shape),
            CrosshairShape.Cross,
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);

    /// <summary>Identifies the <see cref="ShowCenterDot"/> property.</summary>
    public static readonly UiProperty<bool> ShowCenterDotProperty =
        UiProperty.Register<Crosshair, bool>(
            nameof(ShowCenterDot),
            false,
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);

    /// <summary>Identifies the <see cref="CenterDotSize"/> property.</summary>
    public static readonly UiProperty<double> CenterDotSizeProperty =
        UiProperty.Register<Crosshair, double>(
            nameof(CenterDotSize),
            2,
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);

    /// <summary>Identifies the <see cref="UseUiScale"/> property.</summary>
    public static readonly UiProperty<bool> UseUiScaleProperty =
        UiProperty.Register<Crosshair, bool>(
            nameof(UseUiScale),
            false,
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);

    /// <summary>
    /// Initializes a crosshair that centers itself within its layout slot.
    /// </summary>
    public Crosshair()
    {
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
    }

    /// <summary>Gets or sets the crosshair color.</summary>
    public Color Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    /// <summary>Gets or sets the length of each crosshair arm.</summary>
    public double Length
    {
        get => GetValue(LengthProperty);
        set => SetValue(LengthProperty, value);
    }

    /// <summary>Gets or sets the crosshair arm thickness.</summary>
    public double Thickness
    {
        get => GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    /// <summary>Gets or sets the distance between the center and each arm.</summary>
    public double Gap
    {
        get => GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    /// <summary>Gets or sets the crosshair outline color.</summary>
    public Color OutlineColor
    {
        get => GetValue(OutlineColorProperty);
        set => SetValue(OutlineColorProperty, value);
    }

    /// <summary>Gets or sets the outline thickness.</summary>
    public double OutlineThickness
    {
        get => GetValue(OutlineThicknessProperty);
        set => SetValue(OutlineThicknessProperty, value);
    }

    /// <summary>Gets or sets the crosshair arm shape.</summary>
    public CrosshairShape Shape
    {
        get => GetValue(ShapeProperty);
        set => SetValue(ShapeProperty, value);
    }

    /// <summary>Gets or sets whether a center dot is drawn.</summary>
    public bool ShowCenterDot
    {
        get => GetValue(ShowCenterDotProperty);
        set => SetValue(ShowCenterDotProperty, value);
    }

    /// <summary>Gets or sets the center dot side length.</summary>
    public double CenterDotSize
    {
        get => GetValue(CenterDotSizeProperty);
        set => SetValue(CenterDotSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the crosshair geometry uses the UI scale. When false, geometry
    /// property values are physical pixels that stay constant across UI scales.
    /// </summary>
    public bool UseUiScale
    {
        get => GetValue(UseUiScaleProperty);
        set => SetValue(UseUiScaleProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureCore(Size availableSize)
    {
        ValidateGeometry();
        var halfExtent = Math.Max(
            Gap + Length + OutlineThickness,
            (ShowCenterDot ? CenterDotSize / 2 : 0) + OutlineThickness);
        var scale = UseUiScale ? 1 : Screen?.Scale ?? 1;
        var size = halfExtent / scale * 2;
        return new Size(size, size);
    }

    /// <inheritdoc />
    protected override void DrawCore(UiDrawingContext context)
    {
        ValidateGeometry();
        using var origin = context.PushTranslate(
            new Point(LayoutBounds.Width / 2, LayoutBounds.Height / 2));
        using var scale = UseUiScale ? default : context.SetScale(1);

        DrawHorizontalArm(context, left: true);
        DrawHorizontalArm(context, left: false);
        if (Shape is CrosshairShape.Cross or CrosshairShape.InvertedT)
            DrawVerticalArm(context, upward: true);
        if (Shape is CrosshairShape.Cross or CrosshairShape.T)
            DrawVerticalArm(context, upward: false);

        if (ShowCenterDot)
        {
            var center = new Rect(
                -CenterDotSize / 2,
                -CenterDotSize / 2,
                CenterDotSize,
                CenterDotSize);
            DrawOutlinedRectangle(context, center);
        }
    }

    private void DrawHorizontalArm(UiDrawingContext context, bool left)
    {
        var x = left ? -Gap - Length : Gap;
        DrawOutlinedRectangle(context, new Rect(x, -Thickness / 2, Length, Thickness));
    }

    private void DrawVerticalArm(UiDrawingContext context, bool upward)
    {
        var y = upward ? -Gap - Length : Gap;
        DrawOutlinedRectangle(context, new Rect(-Thickness / 2, y, Thickness, Length));
    }

    private void DrawOutlinedRectangle(UiDrawingContext context, Rect bounds)
    {
        var outline = new Rect(
            bounds.X - OutlineThickness,
            bounds.Y - OutlineThickness,
            bounds.Width + OutlineThickness * 2,
            bounds.Height + OutlineThickness * 2);
        context.FillRectangle(outline, OutlineColor);
        context.FillRectangle(bounds, Color);
    }

    private void ValidateGeometry()
    {
        ValidateDimension(Length, nameof(Length));
        ValidateDimension(Thickness, nameof(Thickness));
        ValidateDimension(Gap, nameof(Gap));
        ValidateDimension(OutlineThickness, nameof(OutlineThickness));
        ValidateDimension(CenterDotSize, nameof(CenterDotSize));
    }

    private static void ValidateDimension(double value, string propertyName)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            throw new InvalidOperationException(
                $"Crosshair {propertyName} must be finite and non-negative.");
        }
    }
}

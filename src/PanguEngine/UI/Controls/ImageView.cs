using PanguEngine.ComponentModel;
using PanguEngine.UI.Drawing;
using PanguEngine.UI.Styling;

namespace PanguEngine.UI.Controls;

/// <summary>
/// Displays a shared UI image using retained layout and drawing semantics.
/// </summary>
public sealed class ImageView : UIElement
{
    /// <summary>
    /// Identifies the image source property.
    /// </summary>
    public static readonly Property<Image?> SourceProperty =
        Property.Register<ImageView, Image?>(
            nameof(Source),
            defaultValue: null,
            onChanged: static (node, _, _) => node.InvalidateMeasure());

    /// <summary>
    /// Identifies the image stretch property.
    /// </summary>
    public static readonly Property<ImageStretch> StretchProperty =
        Property.Register<ImageView, ImageStretch>(
            nameof(Stretch),
            ImageStretch.Uniform,
            onChanged: static (node, _, _) => node.InvalidateMeasure(),
            validate: static value => value is ImageStretch.None or ImageStretch.Fill or ImageStretch.Uniform
                or ImageStretch.UniformToFill,
            validationMessage: "Image stretch has an undefined value.");

    /// <summary>
    /// Identifies the source region property.
    /// </summary>
    public static readonly Property<Rect?> SourceRectProperty =
        Property.Register<ImageView, Rect?>(
            nameof(SourceRect),
            defaultValue: null,
            onChanged: static (node, _, _) => node.InvalidateMeasure());

    /// <summary>
    /// Identifies the image sampling mode property.
    /// </summary>
    public static readonly Property<ImageSamplingMode> SamplingModeProperty =
        Property.Register<ImageView, ImageSamplingMode>(
            nameof(SamplingMode),
            ImageSamplingMode.Linear);

    static ImageView()
    {
        CssRegistry.RegisterElement<ImageView>("ImageView");
        CssRegistry.RegisterProperty<ImageView, ImageStretch>(
            "stretch",
            StretchProperty,
            CssValueConverters.ParseImageStretch);
        CssRegistry.RegisterProperty<ImageView, ImageSamplingMode>(
            "sampling-mode",
            SamplingModeProperty,
            CssValueConverters.ParseImageSamplingMode);
    }

    /// <summary>
    /// Initializes an image view.
    /// </summary>
    public ImageView()
    {
    }

    /// <summary>
    /// Gets or sets the shared image source.
    /// </summary>
    public Image? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>
    /// Gets or sets how the image is fitted into the arranged bounds.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is undefined.</exception>
    public ImageStretch Stretch
    {
        get => GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    /// <summary>
    /// Gets or sets the source image pixel region, or null for the full image.
    /// </summary>
    public Rect? SourceRect
    {
        get => GetValue(SourceRectProperty);
        set => SetValue(SourceRectProperty, value);
    }

    /// <summary>
    /// Gets or sets the image interpolation mode.
    /// </summary>
    public ImageSamplingMode SamplingMode
    {
        get => GetValue(SamplingModeProperty);
        set => SetValue(SamplingModeProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureCore(Size availableSize)
    {
        var image = Source;
        if (image is null)
            return Size.Zero;

        var sourceRect = ResolveSourceRect(image);
        if (sourceRect.Width == 0 || sourceRect.Height == 0)
            return Size.Zero;

        if (Stretch == ImageStretch.Fill)
        {
            return new Size(
                double.IsPositiveInfinity(availableSize.Width)
                    ? sourceRect.Width
                    : availableSize.Width,
                double.IsPositiveInfinity(availableSize.Height)
                    ? sourceRect.Height
                    : availableSize.Height);
        }

        var scale = ImageLayout.GetScale(
            availableSize,
            sourceRect.Width,
            sourceRect.Height,
            Stretch);
        return new Size(sourceRect.Width * scale, sourceRect.Height * scale);
    }

    /// <inheritdoc />
    protected override void DrawCore(DrawingContext context)
    {
        var image = Source;
        if (image is null)
            return;

        var sourceRect = ResolveSourceRect(image);
        if (sourceRect.Width == 0 || sourceRect.Height == 0)
            return;

        var viewBounds = new Rect(0, 0, LayoutBounds.Width, LayoutBounds.Height);
        var scene = Scene;
        var useLayoutRounding = scene?.UseLayoutRounding ?? true;
        var scale = scene?.Scale ?? 1;
        var destinationBounds = ImageLayout.GetDestinationBounds(
            viewBounds,
            sourceRect.Width,
            sourceRect.Height,
            Stretch);
        if (useLayoutRounding && Stretch != ImageStretch.Fill)
            destinationBounds = LayoutHelper.RoundLayoutRect(destinationBounds, scale);
        using (context.PushClip(viewBounds))
        {
            context.DrawImage(destinationBounds, image, sourceRect, SamplingMode);
        }
    }

    private Rect ResolveSourceRect(Image image)
    {
        var sourceRect = SourceRect ?? image.FullSourceRect;
        if (!image.ContainsSourceRect(sourceRect))
        {
            throw new InvalidOperationException(
                "ImageView.SourceRect must be contained within the source image.");
        }

        return sourceRect;
    }
}
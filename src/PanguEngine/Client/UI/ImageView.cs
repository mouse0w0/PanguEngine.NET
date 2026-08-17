namespace PanguEngine.Client.UI;

/// <summary>
/// Displays a shared UI image using retained layout and drawing semantics.
/// </summary>
public sealed class ImageView : UiNode
{
    /// <summary>
    /// Identifies the image source property.
    /// </summary>
    public static readonly UiProperty<UiImage?> SourceProperty =
        UiProperty.Register<ImageView, UiImage?>(
            nameof(Source),
            defaultValue: null,
            invalidation: UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);

    /// <summary>
    /// Identifies the image stretch property.
    /// </summary>
    public static readonly UiProperty<ImageStretch> StretchProperty =
        UiProperty.Register<ImageView, ImageStretch>(
            nameof(Stretch),
            ImageStretch.Uniform,
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);

    /// <summary>
    /// Identifies the source region property.
    /// </summary>
    public static readonly UiProperty<Rect?> SourceRectProperty =
        UiProperty.Register<ImageView, Rect?>(
            nameof(SourceRect),
            defaultValue: null,
            invalidation: UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);

    /// <summary>
    /// Identifies the image sampling mode property.
    /// </summary>
    public static readonly UiProperty<ImageSamplingMode> SamplingModeProperty =
        UiProperty.Register<ImageView, ImageSamplingMode>(
            nameof(SamplingMode),
            ImageSamplingMode.Linear,
            UiPropertyInvalidation.Render);

    /// <summary>
    /// Initializes an image view.
    /// </summary>
    public ImageView()
    {
    }

    /// <summary>
    /// Gets or sets the shared image source.
    /// </summary>
    public UiImage? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>
    /// Gets or sets how the image is fitted into the arranged bounds.
    /// </summary>
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

        var scale = GetScale(availableSize, sourceRect.Width, sourceRect.Height, Stretch);
        return new Size(sourceRect.Width * scale, sourceRect.Height * scale);
    }

    /// <inheritdoc />
    protected override void DrawCore(UiDrawingContext context)
    {
        var image = Source;
        if (image is null)
            return;

        var sourceRect = ResolveSourceRect(image);
        if (sourceRect.Width == 0 || sourceRect.Height == 0)
            return;

        var viewBounds = new Rect(0, 0, LayoutBounds.Width, LayoutBounds.Height);
        var screen = Screen;
        var useLayoutRounding = screen?.UseLayoutRounding ?? true;
        var scale = screen?.Scale ?? 1;
        var destinationBounds = GetDestinationBounds(
            viewBounds,
            sourceRect.Width,
            sourceRect.Height,
            Stretch,
            useLayoutRounding,
            scale);
        using (context.PushClip(viewBounds))
        {
            context.DrawImage(destinationBounds, image, sourceRect, SamplingMode);
        }
    }

    private Rect ResolveSourceRect(UiImage image)
    {
        var sourceRect = SourceRect ?? image.FullSourceRect;
        if (!image.ContainsSourceRect(sourceRect))
        {
            throw new InvalidOperationException(
                "ImageView.SourceRect must be contained within the source image.");
        }

        return sourceRect;
    }

    private static double GetScale(
        Size availableSize,
        double sourceWidth,
        double sourceHeight,
        ImageStretch stretch) =>
        stretch switch
        {
            ImageStretch.None => 1,
            ImageStretch.Uniform =>
                GetUniformScale(availableSize, sourceWidth, sourceHeight, useMaximum: false),
            ImageStretch.UniformToFill =>
                GetUniformScale(availableSize, sourceWidth, sourceHeight, useMaximum: true),
            ImageStretch.Fill => 1,
            _ => throw new InvalidOperationException("ImageView.Stretch has an undefined value.")
        };

    private static double GetUniformScale(
        Size availableSize,
        double sourceWidth,
        double sourceHeight,
        bool useMaximum)
    {
        var widthIsInfinite = double.IsPositiveInfinity(availableSize.Width);
        var heightIsInfinite = double.IsPositiveInfinity(availableSize.Height);
        if (widthIsInfinite && heightIsInfinite)
            return 1;
        if (widthIsInfinite)
            return availableSize.Height / sourceHeight;
        if (heightIsInfinite)
            return availableSize.Width / sourceWidth;

        var widthScale = availableSize.Width / sourceWidth;
        var heightScale = availableSize.Height / sourceHeight;
        return useMaximum
            ? Math.Max(widthScale, heightScale)
            : Math.Min(widthScale, heightScale);
    }

    private static Rect GetDestinationBounds(
        Rect viewBounds,
        double sourceWidth,
        double sourceHeight,
        ImageStretch stretch,
        bool useLayoutRounding,
        double layoutScale)
    {
        if (stretch == ImageStretch.Fill)
            return viewBounds;

        var scale = GetScale(
            new Size(viewBounds.Width, viewBounds.Height),
            sourceWidth,
            sourceHeight,
            stretch);
        var width = sourceWidth * scale;
        var height = sourceHeight * scale;
        var bounds = new Rect(
            (viewBounds.Width - width) / 2,
            (viewBounds.Height - height) / 2,
            width,
            height);
        return useLayoutRounding
            ? UiLayoutHelper.RoundLayoutRect(bounds, layoutScale)
            : bounds;
    }
}

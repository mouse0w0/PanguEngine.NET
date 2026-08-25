namespace PanguEngine.Client.UI;

internal static class UiImageLayout
{
    internal static double GetScale(
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
            _ => throw new InvalidOperationException("Image stretch has an undefined value.")
        };

    internal static Rect GetDestinationBounds(
        Rect viewBounds,
        double sourceWidth,
        double sourceHeight,
        ImageStretch stretch)
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
            viewBounds.X + (viewBounds.Width - width) / 2,
            viewBounds.Y + (viewBounds.Height - height) / 2,
            width,
            height);
        return bounds;
    }

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
}

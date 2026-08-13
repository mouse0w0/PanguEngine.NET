using PanguEngine.Resources.Images;

namespace PanguEngine.Client.UI;

/// <summary>
/// Represents an immutable UI image source managed by the UI renderer.
/// </summary>
public sealed class UiImage
{
    private UiImage(
        int pixelWidth,
        int pixelHeight,
        ReadOnlyMemory<byte> cpuPixels)
    {
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        Pixels = cpuPixels;
    }

    /// <summary>
    /// Creates an image by synchronously decoding encoded image data from a stream.
    /// </summary>
    /// <param name="stream">The stream containing encoded image data.</param>
    /// <returns>The decoded UI image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    public static UiImage FromStream(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var image = ImageDecoder.Decode(stream);
        return new UiImage(image.Width, image.Height, image.Pixels);
    }

    /// <summary>
    /// Creates an image by copying tightly packed non-premultiplied RGBA8 pixels.
    /// </summary>
    /// <param name="rgbaPixels">The RGBA8 pixels in row-major order.</param>
    /// <param name="pixelWidth">The image width in pixels.</param>
    /// <param name="pixelHeight">The image height in pixels.</param>
    /// <returns>The UI image containing a copy of the supplied pixels.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when a dimension is not positive or the expected pixel length cannot be represented.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when the pixel length does not match the dimensions.</exception>
    public static UiImage FromRgba(
        ReadOnlySpan<byte> rgbaPixels,
        int pixelWidth,
        int pixelHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelHeight);

        var pixelCount = (long)pixelWidth * pixelHeight;
        if (pixelCount > int.MaxValue / 4)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pixelWidth),
                "The RGBA pixel data is too large to fit in a managed buffer.");
        }

        var expectedLength = pixelCount * 4;
        if (rgbaPixels.Length != expectedLength)
            throw new ArgumentException("RGBA pixel data length does not match the image dimensions.", nameof(rgbaPixels));

        return new UiImage(pixelWidth, pixelHeight, rgbaPixels.ToArray());
    }

    /// <summary>
    /// Gets the image width in pixels.
    /// </summary>
    public int PixelWidth { get; }

    /// <summary>
    /// Gets the image height in pixels.
    /// </summary>
    public int PixelHeight { get; }

    /// <summary>
    /// Gets the tightly packed row-major non-premultiplied RGBA8 sRGB pixels.
    /// </summary>
    internal ReadOnlyMemory<byte> Pixels { get; }

    internal Rect FullSourceRect =>
        new(0, 0, PixelWidth, PixelHeight);

    internal bool ContainsSourceRect(Rect sourceRect) =>
        sourceRect.X >= 0 &&
        sourceRect.Y >= 0 &&
        sourceRect.X <= PixelWidth &&
        sourceRect.Y <= PixelHeight &&
        sourceRect.Width <= PixelWidth - sourceRect.X &&
        sourceRect.Height <= PixelHeight - sourceRect.Y;

}

using System.Runtime.CompilerServices;

namespace PanguEngine.Client.UI;

/// <summary>
/// Represents a brush that fills an area with an image.
/// </summary>
public sealed class ImageBrush : Brush, IEquatable<ImageBrush>
{
    /// <summary>
    /// Initializes an image brush that uses the full source image.
    /// </summary>
    /// <param name="source">The shared image source.</param>
    /// <param name="stretch">How the image is fitted into the filled area.</param>
    /// <param name="samplingMode">The image interpolation mode.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
    public ImageBrush(
        UiImage source,
        ImageStretch stretch = ImageStretch.Fill,
        ImageSamplingMode samplingMode = ImageSamplingMode.Linear)
    {
        ArgumentNullException.ThrowIfNull(source);
        Source = source;
        SourceRect = source.FullSourceRect;
        Stretch = stretch;
        SamplingMode = samplingMode;
    }

    /// <summary>
    /// Initializes an image brush that uses a source image region.
    /// </summary>
    /// <param name="source">The shared image source.</param>
    /// <param name="sourceRect">The source region in image pixel coordinates.</param>
    /// <param name="stretch">How the image is fitted into the filled area.</param>
    /// <param name="samplingMode">The image interpolation mode.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="sourceRect"/> is outside the image.
    /// </exception>
    public ImageBrush(
        UiImage source,
        Rect sourceRect,
        ImageStretch stretch = ImageStretch.Fill,
        ImageSamplingMode samplingMode = ImageSamplingMode.Linear)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.ContainsSourceRect(sourceRect))
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceRect),
                "The image source region must be contained within the image.");
        }

        Source = source;
        SourceRect = sourceRect;
        Stretch = stretch;
        SamplingMode = samplingMode;
    }

    /// <summary>
    /// Gets the shared image source.
    /// </summary>
    public UiImage Source { get; }

    /// <summary>
    /// Gets the source region in image pixel coordinates.
    /// </summary>
    public Rect SourceRect { get; }

    /// <summary>
    /// Gets how the image is fitted into the filled area.
    /// </summary>
    public ImageStretch Stretch { get; }

    /// <summary>
    /// Gets the image interpolation mode.
    /// </summary>
    public ImageSamplingMode SamplingMode { get; }

    /// <inheritdoc />
    public bool Equals(ImageBrush? other) =>
        other is not null &&
        ReferenceEquals(Source, other.Source) &&
        SourceRect == other.SourceRect &&
        Stretch == other.Stretch &&
        SamplingMode == other.SamplingMode;

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is ImageBrush other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        HashCode.Combine(
            RuntimeHelpers.GetHashCode(Source),
            SourceRect,
            Stretch,
            SamplingMode);
}

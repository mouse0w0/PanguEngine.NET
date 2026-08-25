using System.Runtime.CompilerServices;

namespace PanguEngine.Client.UI;

/// <summary>
/// Represents a brush that fills an area with a nine-slice image.
/// </summary>
public sealed class NineSliceImageBrush : Brush, IEquatable<NineSliceImageBrush>
{
    /// <summary>
    /// Initializes a nine-slice image brush that uses the full source image.
    /// </summary>
    /// <param name="source">The shared image source.</param>
    /// <param name="slice">The fixed source-pixel insets.</param>
    /// <param name="samplingMode">The image interpolation mode.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the slice does not leave a positive center source region.
    /// </exception>
    public NineSliceImageBrush(
        UiImage source,
        ImageSlice slice,
        ImageSamplingMode samplingMode = ImageSamplingMode.Linear)
    {
        ArgumentNullException.ThrowIfNull(source);
        var sourceRect = source.FullSourceRect;
        VerifySlice(sourceRect, slice);
        Source = source;
        SourceRect = sourceRect;
        Slice = slice;
        SamplingMode = samplingMode;
    }

    /// <summary>
    /// Initializes a nine-slice image brush that uses a source image region.
    /// </summary>
    /// <param name="source">The shared image source.</param>
    /// <param name="sourceRect">The source region in image pixel coordinates.</param>
    /// <param name="slice">The fixed source-pixel insets relative to <paramref name="sourceRect"/>.</param>
    /// <param name="samplingMode">The image interpolation mode.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the source region is outside the image or the slice does not leave a positive center region.
    /// </exception>
    public NineSliceImageBrush(
        UiImage source,
        Rect sourceRect,
        ImageSlice slice,
        ImageSamplingMode samplingMode = ImageSamplingMode.Linear)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.ContainsSourceRect(sourceRect))
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceRect),
                "The image source region must be contained within the image.");
        }

        VerifySlice(sourceRect, slice);
        Source = source;
        SourceRect = sourceRect;
        Slice = slice;
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
    /// Gets the fixed source-pixel insets.
    /// </summary>
    public ImageSlice Slice { get; }

    /// <summary>
    /// Gets the image interpolation mode.
    /// </summary>
    public ImageSamplingMode SamplingMode { get; }

    /// <inheritdoc />
    public bool Equals(NineSliceImageBrush? other) =>
        other is not null &&
        ReferenceEquals(Source, other.Source) &&
        SourceRect == other.SourceRect &&
        Slice == other.Slice &&
        SamplingMode == other.SamplingMode;

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is NineSliceImageBrush other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        HashCode.Combine(
            RuntimeHelpers.GetHashCode(Source),
            SourceRect,
            Slice,
            SamplingMode);

    private static void VerifySlice(Rect sourceRect, ImageSlice slice)
    {
        if ((long)slice.Left + slice.Right >= sourceRect.Width ||
            (long)slice.Top + slice.Bottom >= sourceRect.Height)
        {
            throw new ArgumentOutOfRangeException(
                nameof(slice),
                "Image slices must leave a positive center region.");
        }
    }
}

using PanguEngine.Client.UI.Drawing.Geometry;

namespace PanguEngine.Client.UI.Drawing;

/// <summary>
/// An immutable path geometry described by absolute move, line, curve and elliptical arc segments.
/// </summary>
/// <remarks>
/// Instances are produced by <see cref="PathBuilder"/> or <see cref="Parse"/> and can be shared by multiple
/// shapes. A geometry never changes after it is created, and a parsed geometry is equivalent to the geometry
/// built by the matching <see cref="PathBuilder"/> commands.
/// </remarks>
public sealed class PathGeometry
{
    private readonly PathSegment[] _segments;

    internal PathGeometry(PathSegment[] segments)
    {
        _segments = segments;
        Bounds = PathGeometryMath.GetBounds(segments);
    }

    /// <summary>
    /// Gets the axis-aligned bounds of the path, or <see cref="Rect.Zero"/> when the path is empty.
    /// </summary>
    public Rect Bounds { get; }

    /// <summary>
    /// Parses an SVG path data string into an immutable geometry.
    /// </summary>
    /// <param name="data">The SVG path data, or null, empty, or whitespace for an empty path.</param>
    /// <returns>The parsed geometry.</returns>
    /// <exception cref="FormatException">
    /// Thrown when the data is syntactically invalid. The message contains the UTF-16 offset of the error.
    /// </exception>
    public static PathGeometry Parse(string? data)
    {
        try
        {
            return new PathGeometry(SvgPathParser.Parse(data));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new FormatException(
                $"SVG path error at offset {data?.Length ?? 0}: Geometry exceeds the finite coordinate range.",
                exception);
        }
    }

    internal FlattenedContour[] Flatten(double tolerance) => PathGeometryMath.Flatten(_segments, tolerance);
}

using PanguEngine.Client.UI.Drawing.Geometry;

namespace PanguEngine.Client.UI.Drawing;

/// <summary>
/// Builds an immutable <see cref="PathGeometry"/> from chained absolute path commands.
/// </summary>
/// <remarks>
/// The first command must be <see cref="MoveTo"/>. After <see cref="Close"/> the current point returns to the
/// start of the current subpath, and later commands continue from there. <see cref="Build"/> returns an
/// independent snapshot, so the builder can keep adding commands without changing geometries that were already
/// built.
/// </remarks>
public sealed class PathBuilder
{
    private readonly List<PathSegment> _segments = [];
    private Point _current;
    private Point _subpathStart;

    /// <summary>
    /// Starts a new subpath at the given point.
    /// </summary>
    /// <param name="point">The subpath start point.</param>
    /// <returns>This builder.</returns>
    public PathBuilder MoveTo(Point point)
    {
        _segments.Add(PathSegmentFactory.Move(point));
        _current = point;
        _subpathStart = point;
        return this;
    }

    /// <summary>
    /// Adds a straight line from the current point to the end point.
    /// </summary>
    /// <param name="end">The line end point.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no move command has been issued yet.</exception>
    public PathBuilder LineTo(Point end)
    {
        RequireStart();
        _segments.Add(PathSegmentFactory.Line(_current, end));
        _current = end;
        return this;
    }

    /// <summary>
    /// Adds a quadratic Bezier curve from the current point to the end point.
    /// </summary>
    /// <param name="control">The quadratic control point.</param>
    /// <param name="end">The curve end point.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no move command has been issued yet.</exception>
    public PathBuilder QuadraticTo(Point control, Point end)
    {
        RequireStart();
        _segments.Add(PathSegmentFactory.Quadratic(_current, control, end));
        _current = end;
        return this;
    }

    /// <summary>
    /// Adds a cubic Bezier curve from the current point to the end point.
    /// </summary>
    /// <param name="control1">The first cubic control point.</param>
    /// <param name="control2">The second cubic control point.</param>
    /// <param name="end">The curve end point.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no move command has been issued yet.</exception>
    public PathBuilder CubicTo(Point control1, Point control2, Point end)
    {
        RequireStart();
        _segments.Add(PathSegmentFactory.Cubic(_current, control1, control2, end));
        _current = end;
        return this;
    }

    /// <summary>
    /// Adds an elliptical arc from the current point to the end point using SVG endpoint parameters.
    /// </summary>
    /// <param name="end">The arc end point.</param>
    /// <param name="radiusX">The ellipse x radius.</param>
    /// <param name="radiusY">The ellipse y radius.</param>
    /// <param name="rotationDegrees">The ellipse rotation in degrees.</param>
    /// <param name="largeArc">Whether the arc spans more than 180 degrees.</param>
    /// <param name="sweepClockwise">Whether the arc is drawn in the positive angle direction.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no move command has been issued yet.</exception>
    public PathBuilder ArcTo(
        Point end,
        double radiusX,
        double radiusY,
        double rotationDegrees,
        bool largeArc,
        bool sweepClockwise)
    {
        RequireStart();
        var segment = PathSegmentFactory.Arc(_current, end, radiusX, radiusY, rotationDegrees, largeArc, sweepClockwise);
        if (segment is { } value)
            _segments.Add(value);
        _current = end;
        return this;
    }

    /// <summary>
    /// Closes the current subpath and moves the current point back to its start.
    /// </summary>
    /// <returns>This builder.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no move command has been issued yet.</exception>
    public PathBuilder Close()
    {
        RequireStart();
        _segments.Add(PathSegmentFactory.Close(_current, _subpathStart));
        _current = _subpathStart;
        return this;
    }

    /// <summary>
    /// Builds an immutable snapshot of the commands added so far.
    /// </summary>
    /// <returns>The built geometry.</returns>
    public PathGeometry Build() => new(_segments.ToArray());

    private void RequireStart()
    {
        if (_segments.Count == 0)
            throw new InvalidOperationException("The first path command must be MoveTo.");
    }
}

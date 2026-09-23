namespace PanguEngine.Client.UI.Drawing.Geometry;

internal static class PathSegmentFactory
{
    internal static PathSegment Move(Point point) =>
        new(PathSegmentKind.Move, point, point, default, default, 0, 0, 0, 0);

    internal static PathSegment Line(Point start, Point end) =>
        new(PathSegmentKind.Line, start, end, default, default, 0, 0, 0, 0);

    internal static PathSegment Quadratic(Point start, Point control, Point end) =>
        new(PathSegmentKind.Quadratic, start, end, control, default, 0, 0, 0, 0);

    internal static PathSegment Cubic(Point start, Point control1, Point control2, Point end) =>
        new(PathSegmentKind.Cubic, start, end, control1, control2, 0, 0, 0, 0);

    internal static PathSegment Close(Point start, Point end) =>
        new(PathSegmentKind.Close, start, end, default, default, 0, 0, 0, 0);

    internal static PathSegment? Arc(
        Point start,
        Point end,
        double radiusX,
        double radiusY,
        double rotationDegrees,
        bool largeArc,
        bool sweepClockwise)
    {
        radiusX = Math.Abs(radiusX);
        radiusY = Math.Abs(radiusY);
        if (end == start)
            return null;
        if (radiusX == 0 || radiusY == 0)
            return Line(start, end);

        var rotation = rotationDegrees % 360 * (Math.PI / 180);
        var cos = Math.Cos(rotation);
        var sin = Math.Sin(rotation);
        var dx = start.X / 2 - end.X / 2;
        var dy = start.Y / 2 - end.Y / 2;
        var x = cos * dx + sin * dy;
        var y = -sin * dx + cos * dy;
        var nx = x / radiusX;
        var ny = y / radiusY;
        var magnitude = Math.Max(Math.Abs(nx), Math.Abs(ny));
        if (!double.IsFinite(magnitude))
            throw new ArgumentOutOfRangeException(nameof(radiusX), "The arc exceeds the supported coordinate range.");
        if (magnitude == 0)
            return Line(start, end);

        var norm = Math.Sqrt(Math.Pow(nx / magnitude, 2) + Math.Pow(ny / magnitude, 2));
        var length = magnitude * norm;
        if (length > 1)
        {
            radiusX *= length;
            radiusY *= length;
        }

        var large = largeArc ? 1 : 0;
        var sweep = sweepClockwise ? 1 : 0;
        var factor = (large == sweep ? -1 : 1) * Math.Sqrt(Math.Max(0, 1 - Math.Min(1, length) * Math.Min(1, length)));
        var cx = factor * radiusX * (ny / magnitude / norm);
        var cy = -factor * radiusY * (nx / magnitude / norm);
        var center = new Point(
            cos * cx - sin * cy + start.X / 2 + end.X / 2,
            sin * cx + cos * cy + start.Y / 2 + end.Y / 2);
        var ux = (x - cx) / radiusX;
        var uy = (y - cy) / radiusY;
        var vx = (-x - cx) / radiusX;
        var vy = (-y - cy) / radiusY;
        var startAngle = Math.Atan2(uy, ux);
        var sweepAngle = Math.Atan2(ux * vy - uy * vx, ux * vx + uy * vy);
        if (sweep != 0 && sweepAngle < 0)
            sweepAngle += Math.PI * 2;
        if (sweep == 0 && sweepAngle > 0)
            sweepAngle -= Math.PI * 2;

        return new PathSegment(
            PathSegmentKind.Arc,
            start,
            end,
            center,
            new Point(rotation, 0),
            radiusX,
            radiusY,
            startAngle,
            sweepAngle);
    }
}

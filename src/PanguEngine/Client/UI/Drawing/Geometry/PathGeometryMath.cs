namespace PanguEngine.Client.UI.Drawing.Geometry;

internal static class PathGeometryMath
{
    internal static Rect GetBounds(PathSegment[] segments)
    {
        if (segments.Length == 0)
            return Rect.Zero;
        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;
        foreach (var segment in segments)
        {
            foreach (var t in Extrema(segment))
            {
                var point = segment.Evaluate(t);
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }
        }

        return double.IsFinite(minX)
            ? new Rect(minX, minY, maxX - minX, maxY - minY)
            : Rect.Zero;
    }

    internal static FlattenedContour[] Flatten(PathSegment[] segments, double tolerance)
    {
        if (segments.Length == 0)
            return [];
        if (!double.IsFinite(tolerance) || tolerance <= 0)
            tolerance = 0.25;

        var contours = new List<FlattenedContour>();
        var points = new List<Point>();
        foreach (var segment in segments)
        {
            if (segment.Kind == PathSegmentKind.Move)
            {
                if (points.Count > 0)
                    contours.Add(new FlattenedContour(points.ToArray(), false));
                points = [segment.End];
                continue;
            }

            if (segment.Kind == PathSegmentKind.Close)
            {
                if (points.Count > 0)
                    contours.Add(new FlattenedContour(points.ToArray(), true));
                points = [];
                continue;
            }

            if (points.Count == 0)
                points.Add(segment.Start);
            FlattenSegment(segment, tolerance, points);
        }

        if (points.Count > 0)
            contours.Add(new FlattenedContour(points.ToArray(), false));
        return contours.ToArray();
    }

    private static void FlattenSegment(PathSegment s, double tolerance, List<Point> output)
    {
        var count = s.Kind switch
        {
            PathSegmentKind.Line => 1,
            PathSegmentKind.Quadratic => CurveCount(tolerance, s.Start, s.Control1, s.End),
            PathSegmentKind.Cubic => CurveCount(tolerance, s.Start, s.Control1, s.Control2, s.End),
            PathSegmentKind.Arc => SubdivisionCount(Math.Abs(s.Sweep) / ArcStep(tolerance, Math.Max(s.RadiusX, s.RadiusY))),
            _ => 0
        };
        for (var index = 1; index <= count; index++)
        {
            var t = (double)index / count;
            output.Add(s.Evaluate(t));
        }
    }

    private static int CurveCount(double tolerance, params Point[] points)
    {
        var second = 0d;
        for (var index = 1; index < points.Length - 1; index++)
        {
            var x = points[index - 1].X - 2 * points[index].X + points[index + 1].X;
            var y = points[index - 1].Y - 2 * points[index].Y + points[index + 1].Y;
            second = Math.Max(second, Math.Sqrt(x * x + y * y));
        }
        var degree = points.Length - 1;
        return SubdivisionCount(Math.Sqrt(degree * (degree - 1) * second / (8 * tolerance)));
    }

    private static int SubdivisionCount(double count)
    {
        if (!double.IsFinite(count) || count > 1_048_576)
            throw new InvalidOperationException("Path subdivision exceeds the supported segment count at the requested tolerance.");
        return Math.Max(1, (int)Math.Ceiling(count));
    }

    private static double ArcStep(double tolerance, double radius)
    {
        var ratio = tolerance / radius;
        return ratio < 1e-6 ? 2 * Math.Sqrt(2 * ratio) : 2 * Math.Acos(Math.Clamp(1 - ratio, -1, 1));
    }

    private static IEnumerable<double> Extrema(PathSegment s)
    {
        yield return 0;
        yield return 1;
        for (var axis = 0; axis < 2; axis++)
        {
            var p0 = axis == 0 ? s.Start.X : s.Start.Y;
            var p1 = axis == 0 ? s.Control1.X : s.Control1.Y;
            var p2 = axis == 0 ? s.Control2.X : s.Control2.Y;
            var p3 = axis == 0 ? s.End.X : s.End.Y;
            if (s.Kind == PathSegmentKind.Quadratic)
            {
                var denominator = p0 - 2 * p1 + p3;
                var t = (p0 - p1) / denominator;
                if (t > 0 && t < 1) yield return t;
            }
            else if (s.Kind == PathSegmentKind.Cubic)
            {
                var a = -p0 + 3 * p1 - 3 * p2 + p3;
                var b = 2 * (p0 - 2 * p1 + p2);
                var c = p1 - p0;
                if (a == 0)
                {
                    var t = -c / b;
                    if (t > 0 && t < 1) yield return t;
                }
                else
                {
                    var discriminant = b * b - 4 * a * c;
                    if (discriminant < 0) continue;
                    var root = Math.Sqrt(discriminant);
                    var t1 = (-b + root) / (2 * a);
                    var t2 = (-b - root) / (2 * a);
                    if (t1 > 0 && t1 < 1) yield return t1;
                    if (t2 > 0 && t2 < 1) yield return t2;
                }
            }
            else if (s.Kind == PathSegmentKind.Arc)
            {
                var phi = s.Control2.X;
                var angle = axis == 0
                    ? Math.Atan2(-s.RadiusY * Math.Sin(phi), s.RadiusX * Math.Cos(phi))
                    : Math.Atan2(s.RadiusY * Math.Cos(phi), s.RadiusX * Math.Sin(phi));
                for (var k = -3; k <= 3; k++)
                {
                    var t = (angle + k * Math.PI - s.Rotation) / s.Sweep;
                    if (t > 0 && t < 1) yield return t;
                }
            }
        }
    }
}

namespace PanguEngine.Client.UI.Drawing.Geometry;

/// <summary>
/// Expands stroke centerlines into solid polygons for segment bodies, joins and caps.
/// </summary>
internal static class UiStrokeOutlineBuilder
{
    private const double CollinearEpsilon = 1e-12;
    private const int MinArcSegments = 4;
    private const int MaxArcSegments = 1 << 20;

    /// <summary>
    /// Builds one solid polygon per segment, join and cap, all in counterclockwise orientation.
    /// </summary>
    internal static List<Point[]> Build(FlattenedContour[] contours, double thickness,
        StrokeLineCap cap, StrokeLineJoin join, double miterLimit, double tolerance)
    {
        double half = thickness * 0.5;
        int fullCircleSegments = ArcSegmentsForFullCircle(half, tolerance);
        var polygons = new List<Point[]>();

        foreach (var contour in contours)
        {
            var points = UiShapeTessellator.CleanContour(contour.Points, contour.Closed);
            if (contour.Closed)
                BuildClosed(points, half, join, miterLimit, fullCircleSegments, polygons);
            else if (contour.Points.Length >= 2)
                BuildOpen(points, half, cap, join, miterLimit, fullCircleSegments, polygons);
        }

        return polygons;
    }

    private static void BuildClosed(List<Point> points, double half, StrokeLineJoin join,
        double miterLimit, int fullCircleSegments, List<Point[]> polygons)
    {
        int count = points.Count;
        if (count < 2)
            return;

        for (int i = 0; i < count; i++)
            AddSegment(points[i], points[(i + 1) % count], half, polygons);

        for (int i = 0; i < count; i++)
        {
            var previous = points[(i + count - 1) % count];
            var current = points[i];
            var next = points[(i + 1) % count];
            AddJoin(previous, current, next, half, join, miterLimit, fullCircleSegments, polygons);
        }
    }

    private static void BuildOpen(List<Point> points, double half, StrokeLineCap cap,
        StrokeLineJoin join, double miterLimit, int fullCircleSegments, List<Point[]> polygons)
    {
        int count = points.Count;
        if (count == 0)
            return;

        if (count == 1)
        {
            AddPointCap(points[0], half, cap, fullCircleSegments, polygons);
            return;
        }

        for (int i = 0; i < count - 1; i++)
            AddSegment(points[i], points[i + 1], half, polygons);

        for (int i = 1; i < count - 1; i++)
            AddJoin(points[i - 1], points[i], points[i + 1], half, join, miterLimit, fullCircleSegments, polygons);

        AddCap(points[0], points[1], half, cap, fullCircleSegments, polygons);
        AddCap(points[^1], points[^2], half, cap, fullCircleSegments, polygons);
    }

    private static void AddSegment(Point from, Point to, double half, List<Point[]> polygons)
    {
        double dx = to.X - from.X;
        double dy = to.Y - from.Y;
        double length = Math.Sqrt(dx * dx + dy * dy);
        if (length <= 0.0)
            return;

        double nx = -dy / length * half;
        double ny = dx / length * half;

        polygons.Add(new[]
        {
            new Point(from.X + nx, from.Y + ny),
            new Point(to.X + nx, to.Y + ny),
            new Point(to.X - nx, to.Y - ny),
            new Point(from.X - nx, from.Y - ny)
        });
    }

    private static void AddJoin(Point previous, Point current, Point next, double half,
        StrokeLineJoin join, double miterLimit, int fullCircleSegments, List<Point[]> polygons)
    {
        double d0x = current.X - previous.X;
        double d0y = current.Y - previous.Y;
        double d1x = next.X - current.X;
        double d1y = next.Y - current.Y;
        double length0 = Math.Sqrt(d0x * d0x + d0y * d0y);
        double length1 = Math.Sqrt(d1x * d1x + d1y * d1y);
        if (length0 <= 0.0 || length1 <= 0.0)
            return;

        d0x /= length0;
        d0y /= length0;
        d1x /= length1;
        d1y /= length1;

        double cross = d0x * d1y - d0y * d1x;
        if (Math.Abs(cross) <= CollinearEpsilon)
        {
            if (join == StrokeLineJoin.Round && d0x * d1x + d0y * d1y < 0.0)
                AddReversalRoundJoin(current, d0x, d0y, half, fullCircleSegments, polygons);
            return;
        }

        double sign = cross > 0.0 ? -1.0 : 1.0;
        double o0x = -d0y * sign;
        double o0y = d0x * sign;
        double o1x = -d1y * sign;
        double o1y = d1x * sign;

        var corner0 = new Point(current.X + o0x * half, current.Y + o0y * half);
        var corner1 = new Point(current.X + o1x * half, current.Y + o1y * half);

        switch (join)
        {
            case StrokeLineJoin.Round:
            {
                double startAngle = Math.Atan2(o0y, o0x);
                double endAngle = Math.Atan2(o1y, o1x);
                double sweep = ShortestAngle(startAngle, endAngle);
                var round = new List<Point> { current };
                AppendArc(round, current, half, startAngle, sweep, fullCircleSegments);
                polygons.Add(round.ToArray());
                break;
            }
            case StrokeLineJoin.Bevel:
                polygons.Add(new[] { current, corner0, corner1 });
                break;
            default:
            {
                double t = ((corner1.X - corner0.X) * d1y - (corner1.Y - corner0.Y) * d1x) / cross;
                double miterX = corner0.X + d0x * t;
                double miterY = corner0.Y + d0y * t;
                bool finiteMiter = double.IsFinite(miterX) && double.IsFinite(miterY);
                double ratio = finiteMiter ? Distance(miterX, miterY, current.X, current.Y) / half : double.PositiveInfinity;
                if (finiteMiter && ratio <= miterLimit)
                    polygons.Add(new[] { current, corner0, new Point(miterX, miterY), corner1 });
                else
                    polygons.Add(new[] { current, corner0, corner1 });
                break;
            }
        }
    }

    private static void AddReversalRoundJoin(Point current, double directionX, double directionY,
        double half, int fullCircleSegments, List<Point[]> polygons)
    {
        double startAngle = Math.Atan2(directionX, -directionY);
        var round = new List<Point> { current };
        AppendArc(round, current, half, startAngle, -Math.PI, fullCircleSegments);
        polygons.Add(round.ToArray());
    }

    private static void AddCap(Point endpoint, Point neighbor, double half, StrokeLineCap cap,
        int fullCircleSegments, List<Point[]> polygons)
    {
        if (cap == StrokeLineCap.Butt)
            return;

        double outwardX = endpoint.X - neighbor.X;
        double outwardY = endpoint.Y - neighbor.Y;
        double length = Math.Sqrt(outwardX * outwardX + outwardY * outwardY);
        if (length <= 0.0)
            return;

        outwardX /= length;
        outwardY /= length;
        double nx = -outwardY;
        double ny = outwardX;

        if (cap == StrokeLineCap.Square)
        {
            polygons.Add(new[]
            {
                new Point(endpoint.X + nx * half, endpoint.Y + ny * half),
                new Point(endpoint.X - nx * half, endpoint.Y - ny * half),
                new Point(endpoint.X - nx * half + outwardX * half, endpoint.Y - ny * half + outwardY * half),
                new Point(endpoint.X + nx * half + outwardX * half, endpoint.Y + ny * half + outwardY * half)
            });
            return;
        }

        var round = new List<Point> { endpoint };
        double startAngle = Math.Atan2(ny, nx);
        AppendArc(round, endpoint, half, startAngle, -Math.PI, fullCircleSegments);
        polygons.Add(round.ToArray());
    }

    private static void AddPointCap(Point point, double half, StrokeLineCap cap,
        int fullCircleSegments, List<Point[]> polygons)
    {
        if (cap == StrokeLineCap.Butt)
            return;

        if (cap == StrokeLineCap.Square)
        {
            polygons.Add(new[]
            {
                new Point(point.X + half, point.Y + half),
                new Point(point.X - half, point.Y + half),
                new Point(point.X - half, point.Y - half),
                new Point(point.X + half, point.Y - half)
            });
            return;
        }

        var round = new List<Point>();
        AppendArc(round, point, half, 0.0, 2.0 * Math.PI, fullCircleSegments);
        polygons.Add(round.ToArray());
    }

    private static void AppendArc(List<Point> points, Point center, double radius,
        double startAngle, double sweep, int fullCircleSegments)
    {
        int segments = Math.Max(1, (int)Math.Ceiling(fullCircleSegments * Math.Abs(sweep) / (2.0 * Math.PI)));
        bool fullCircle = Math.Abs(Math.Abs(sweep) - 2.0 * Math.PI) <= 1e-9;
        int last = fullCircle ? segments - 1 : segments;
        for (int i = 0; i <= last; i++)
        {
            double angle = startAngle + sweep * i / segments;
            points.Add(new Point(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius));
        }
    }

    private static int ArcSegmentsForFullCircle(double radius, double tolerance)
    {
        if (radius <= 0.0)
            return MinArcSegments;

        double effectiveTolerance = tolerance;
        if (!(effectiveTolerance > 0.0) || !double.IsFinite(effectiveTolerance))
            effectiveTolerance = radius;

        if (effectiveTolerance >= radius * 2.0)
            return MinArcSegments;

        double cosine = 1.0 - effectiveTolerance / radius;
        double angle = cosine >= 1.0
            ? Math.Sqrt(2.0 * effectiveTolerance / radius)
            : Math.Acos(cosine);

        double count = Math.PI / angle;
        if (!(count < MaxArcSegments))
            return MaxArcSegments;
        return Math.Max(MinArcSegments, (int)Math.Ceiling(count));
    }

    private static double ShortestAngle(double from, double to)
    {
        double delta = to - from;
        if (delta > Math.PI)
            delta -= 2.0 * Math.PI;
        else if (delta < -Math.PI)
            delta += 2.0 * Math.PI;
        return delta;
    }

    private static double Distance(double ax, double ay, double bx, double by)
    {
        double dx = ax - bx;
        double dy = ay - by;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

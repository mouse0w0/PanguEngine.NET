namespace PanguEngine.Client.UI.Drawing.Geometry;

/// <summary>Describes a normalized rounded rectangle with independent elliptical corners.</summary>
internal readonly record struct UiRoundedRectangle
{
    internal UiRoundedRectangle(Rect bounds, CornerRadius radius)
        : this(bounds, new Point(radius.TopLeft, radius.TopLeft), new Point(radius.TopRight, radius.TopRight),
            new Point(radius.BottomRight, radius.BottomRight), new Point(radius.BottomLeft, radius.BottomLeft)) { }

    internal UiRoundedRectangle(Rect bounds, Point topLeft, Point topRight, Point bottomRight, Point bottomLeft)
    {
        Bounds = bounds;
        topLeft = SquareIfDegenerate(topLeft);
        topRight = SquareIfDegenerate(topRight);
        bottomRight = SquareIfDegenerate(bottomRight);
        bottomLeft = SquareIfDegenerate(bottomLeft);
        var factor = 1d;
        Limit(bounds.Width, topLeft.X, topRight.X, ref factor);
        Limit(bounds.Width, bottomLeft.X, bottomRight.X, ref factor);
        Limit(bounds.Height, topLeft.Y, bottomLeft.Y, ref factor);
        Limit(bounds.Height, topRight.Y, bottomRight.Y, ref factor);
        TopLeft = Scale(topLeft, factor);
        TopRight = Scale(topRight, factor);
        BottomRight = Scale(bottomRight, factor);
        BottomLeft = Scale(bottomLeft, factor);
    }

    internal Rect Bounds { get; }
    internal Point TopLeft { get; }
    internal Point TopRight { get; }
    internal Point BottomRight { get; }
    internal Point BottomLeft { get; }

    internal UiRoundedRectangle Deflate(Rect inner)
    {
        var left = inner.X - Bounds.X;
        var top = inner.Y - Bounds.Y;
        var right = Bounds.X + Bounds.Width - inner.X - inner.Width;
        var bottom = Bounds.Y + Bounds.Height - inner.Y - inner.Height;
        return new UiRoundedRectangle(inner, Subtract(TopLeft, left, top), Subtract(TopRight, right, top),
            Subtract(BottomRight, right, bottom), Subtract(BottomLeft, left, bottom));
    }

    internal bool Contains(Point point)
    {
        var x = point.X - Bounds.X;
        var y = point.Y - Bounds.Y;
        if (x < 0 || y < 0 || x >= Bounds.Width || y >= Bounds.Height)
            return false;
        return InCorner(x, y, TopLeft) && InCorner(Bounds.Width - x, y, TopRight) &&
               InCorner(Bounds.Width - x, Bounds.Height - y, BottomRight) &&
               InCorner(x, Bounds.Height - y, BottomLeft);
    }

    internal Point[] GetContour(double scale)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
            return [];
        var points = new List<Point>();
        var left = Bounds.X;
        var top = Bounds.Y;
        var right = left + Bounds.Width;
        var bottom = top + Bounds.Height;
        AddCorner(points, new Point(left, top), TopLeft, Math.PI, scale);
        AddCorner(points, new Point(right, top), TopRight, Math.PI * 1.5, scale);
        AddCorner(points, new Point(right, bottom), BottomRight, 0, scale);
        AddCorner(points, new Point(left, bottom), BottomLeft, Math.PI / 2, scale);
        if (points.Count > 1 && points[0] == points[^1])
            points.RemoveAt(points.Count - 1);
        return points.ToArray();
    }

    private static void AddCorner(List<Point> points, Point corner, Point radius, double angle, double scale)
    {
        if (radius == Point.Zero)
        {
            Add(points, corner);
            return;
        }
        var sx = angle == Math.PI || angle == Math.PI / 2 ? 1 : -1;
        var sy = angle >= Math.PI ? 1 : -1;
        var center = new Point(corner.X + sx * radius.X, corner.Y + sy * radius.Y);
        var physicalRadius = Math.Max(radius.X, radius.Y) * scale;
        var step = 2 * Math.Acos(Math.Clamp(1 - 0.25 / physicalRadius, -1, 1));
        var segments = Math.Clamp((int)Math.Ceiling((Math.PI / 2) / Math.Max(step, Math.PI / 16384)), 1, 8192);
        for (var i = 0; i <= segments; i++)
        {
            var a = angle + Math.PI / 2 * i / segments;
            var cosine = i == 0 || i == segments ? Math.Round(Math.Cos(a)) : Math.Cos(a);
            var sine = i == 0 || i == segments ? Math.Round(Math.Sin(a)) : Math.Sin(a);
            Add(points, new Point(center.X + radius.X * cosine, center.Y + radius.Y * sine));
        }
    }

    private static void Add(List<Point> points, Point point)
    {
        if (points.Count == 0 || points[^1] != point)
            points.Add(point);
    }

    private static bool InCorner(double x, double y, Point radius)
    {
        if (radius == Point.Zero || x >= radius.X || y >= radius.Y)
            return true;
        var dx = (x - radius.X) / radius.X;
        var dy = (y - radius.Y) / radius.Y;
        return dx * dx + dy * dy <= 1;
    }

    private static Point SquareIfDegenerate(Point value) => value.X <= 0 || value.Y <= 0 ? Point.Zero : value;
    private static Point Scale(Point value, double factor) => new(value.X * factor, value.Y * factor);
    private static Point Subtract(Point value, double x, double y) => new(Math.Max(0, value.X - x), Math.Max(0, value.Y - y));

    private static void Limit(double length, double first, double second, ref double factor)
    {
        var maximum = Math.Max(first, second);
        if (maximum > 0)
            factor = Math.Min(factor, (length / maximum) / (first / maximum + second / maximum));
    }
}

/// <summary>Describes a rounded region and retains its geometric clip for compatible drawing.</summary>
internal sealed class UiRoundedClipGeometry(
    UiRoundedRectangle outer, UiRoundedRectangle? inner = null, UiRoundedRectangle? constraint = null)
{
    private double _scale;
    private Point[] _outer = [];
    private Point[]? _inner;
    private Point[]? _constraint;
    private PhysicalClipEntry? _physicalClip;
    internal UiRoundedRectangle Outer { get; } = outer;
    internal UiRoundedRectangle? Inner { get; } = inner;
    internal UiRoundedRectangle? Constraint { get; } = constraint;

    internal UiConvexClip GetPhysicalClip(double scale, double x, double y, UiConvexClip? ancestor)
    {
        if (_physicalClip is { } cached && cached.Scale == scale && cached.X == x && cached.Y == y &&
            (ReferenceEquals(cached.Ancestor, ancestor) ||
            (cached.Ancestor is not null && ancestor is not null && cached.Ancestor.ContentEquals(ancestor))))
            return cached.Result;
        var contours = GetContours(scale);
        var outer = Transform(contours.Outer, scale, x, y);
        var inner = contours.Inner is { } hole ? Transform(hole, scale, x, y) : null;
        var clip = ancestor is not null
            ? ancestor.Intersect(outer, inner)
            : new UiConvexClip(outer, inner is { Length: >= 3 } ? [inner] : []);
        if (contours.Constraint is { } boundary)
            clip = clip.Intersect(Transform(boundary, scale, x, y));
        _physicalClip = new PhysicalClipEntry(scale, x, y, ancestor, clip);
        return clip;
    }

    private static Point[] Transform(Point[] points, double scale, double x, double y)
    {
        var transformed = new Point[points.Length];
        for (var i = 0; i < points.Length; i++)
        {
            var point = new Point(x + points[i].X * scale, y + points[i].Y * scale);
            if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
                throw new InvalidOperationException("UI drawing produced a non-finite coordinate or scale.");
            transformed[i] = point;
        }
        return transformed;
    }

    private sealed record PhysicalClipEntry(
        double Scale, double X, double Y, UiConvexClip? Ancestor, UiConvexClip Result);

    internal (Point[] Outer, Point[]? Inner, Point[]? Constraint) GetContours(double scale)
    {
        if (_scale != scale)
        {
            _outer = Outer.GetContour(scale);
            _inner = Inner?.GetContour(scale);
            _constraint = Constraint?.GetContour(scale);
            _scale = scale;
        }
        return (_outer, _inner, _constraint);
    }
}

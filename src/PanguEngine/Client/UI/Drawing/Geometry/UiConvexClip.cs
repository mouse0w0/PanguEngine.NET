namespace PanguEngine.Client.UI.Drawing.Geometry;

/// <summary>Represents a physical convex clip with optional excluded convex regions.</summary>
internal sealed class UiConvexClip
{
    internal UiConvexClip(Point[] outer, Point[][]? holes = null)
    {
        Outer = outer;
        Holes = holes ?? [];
    }

    internal Point[] Outer { get; }
    internal Point[][] Holes { get; }

    internal UiConvexClip Intersect(Point[] outer, Point[]? hole = null)
    {
        var intersection = Outer.AsSpan().SequenceEqual(outer) ? Outer : IntersectPolygon(Outer, outer);
        var holes = Holes;
        if (hole is { Length: >= 3 })
            holes = [.. holes, hole];
        return new UiConvexClip(intersection, holes);
    }

    internal bool ContentEquals(UiConvexClip other)
    {
        if (!Outer.AsSpan().SequenceEqual(other.Outer) || Holes.Length != other.Holes.Length)
            return false;
        for (var i = 0; i < Holes.Length; i++)
            if (!Holes[i].AsSpan().SequenceEqual(other.Holes[i]))
                return false;
        return true;
    }

    internal bool Contains(Rect bounds)
    {
        if (Outer.Length < 3)
            return false;
        var rectangle = Rectangle(bounds);
        foreach (var point in rectangle)
            if (!ContainsPoint(Outer, point))
                return false;
        foreach (var hole in Holes)
            if (IntersectPolygon(rectangle, hole).Length >= 3)
                return false;
        return true;
    }

    internal List<Point[]> Apply(Point[] polygon)
    {
        var intersection = IntersectPolygon(polygon, Outer);
        var pieces = new List<Point[]>();
        if (intersection.Length < 3)
            return pieces;
        pieces.Add(intersection);
        foreach (var hole in Holes)
        {
            var next = new List<Point[]>();
            foreach (var piece in pieces)
                Subtract(piece, hole, next);
            pieces = next;
        }
        return pieces;
    }

    internal UiTriangleMesh Apply(UiTriangleMesh mesh, double scale, double x, double y)
    {
        var vertices = new List<Point>();
        var indices = new List<uint>();
        var source = mesh.Vertices;
        var triangles = mesh.Indices;
        for (var i = 0; i < triangles.Length; i += 3)
        {
            var triangle = new Point[3];
            for (var j = 0; j < 3; j++)
            {
                var point = source[triangles[i + j]];
                triangle[j] = new Point(x + point.X * scale, y + point.Y * scale);
            }
            foreach (var polygon in Apply(triangle))
                AppendFan(polygon, vertices, indices);
        }
        return indices.Count == 0 ? UiTriangleMesh.Empty : new UiTriangleMesh(vertices.ToArray(), indices.ToArray());
    }

    internal static Point[] Rectangle(Rect bounds) =>
    [
        new(bounds.X, bounds.Y), new(bounds.X + bounds.Width, bounds.Y),
        new(bounds.X + bounds.Width, bounds.Y + bounds.Height), new(bounds.X, bounds.Y + bounds.Height)
    ];

    private static void AppendFan(Point[] polygon, List<Point> vertices, List<uint> indices)
    {
        var start = checked((uint)vertices.Count);
        vertices.AddRange(polygon);
        for (var i = 1; i + 1 < polygon.Length; i++)
        {
            indices.Add(start);
            indices.Add(start + (uint)i);
            indices.Add(start + (uint)i + 1);
        }
    }

    internal static Point[] IntersectPolygon(Point[] polygon, Point[] clip)
    {
        if (clip.Length < 3)
            return [];
        foreach (var (a, b) in Edges(clip))
        {
            polygon = ClipEdge(polygon, a, b, true);
            if (polygon.Length < 3)
                return [];
        }
        return polygon;
    }

    private static void Subtract(Point[] polygon, Point[] hole, List<Point[]> result)
    {
        foreach (var (a, b) in Edges(hole))
        {
            var outside = ClipEdge(polygon, a, b, false);
            if (outside.Length >= 3)
                result.Add(outside);
            polygon = ClipEdge(polygon, a, b, true);
            if (polygon.Length < 3)
                return;
        }
    }

    private static IEnumerable<(Point A, Point B)> Edges(Point[] polygon)
    {
        for (var i = 0; i < polygon.Length; i++)
            yield return (polygon[i], polygon[(i + 1) % polygon.Length]);
    }

    private static bool ContainsPoint(Point[] polygon, Point point)
    {
        foreach (var (a, b) in Edges(polygon))
            if (Side(point, a, b) < 0)
                return false;
        return true;
    }

    private static Point[] ClipEdge(Point[] polygon, Point a, Point b, bool inside)
    {
        if (polygon.Length == 0)
            return [];
        var result = new List<Point>(polygon.Length + 1);
        var previous = polygon[^1];
        var previousDistance = Side(previous, a, b);
        var previousInside = inside ? previousDistance >= 0 : previousDistance <= 0;
        foreach (var current in polygon)
        {
            var distance = Side(current, a, b);
            var currentInside = inside ? distance >= 0 : distance <= 0;
            if (previousInside != currentInside)
            {
                var t = previousDistance / (previousDistance - distance);
                Add(result, new Point(previous.X + (current.X - previous.X) * t,
                    previous.Y + (current.Y - previous.Y) * t));
            }
            if (currentInside)
                Add(result, current);
            previous = current;
            previousDistance = distance;
            previousInside = currentInside;
        }
        if (result.Count > 1 && result[0] == result[^1])
            result.RemoveAt(result.Count - 1);
        return result.ToArray();
    }

    private static void Add(List<Point> points, Point point)
    {
        if (points.Count == 0 || points[^1] != point)
            points.Add(point);
    }

    private static double Side(Point point, Point a, Point b) =>
        (b.X - a.X) * (point.Y - a.Y) - (b.Y - a.Y) * (point.X - a.X);
}

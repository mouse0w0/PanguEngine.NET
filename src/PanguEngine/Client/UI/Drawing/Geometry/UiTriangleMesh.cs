namespace PanguEngine.Client.UI.Drawing.Geometry;

/// <summary>
/// An immutable triangle mesh in logical pixels used for shape geometry, hit testing and drawing.
/// </summary>
internal sealed class UiTriangleMesh
{
    private const double DegenerateAreaRatio = 1e-12;

    private readonly Point[] _vertices;
    private readonly uint[] _indices;

    /// <summary>
    /// Initializes a mesh from triangle vertices and indices and computes its bounds.
    /// </summary>
    internal UiTriangleMesh(Point[] vertices, uint[] indices)
    {
        _vertices = vertices;
        _indices = indices;
        Bounds = ComputeBounds(vertices);
    }

    private UiTriangleMesh(Point[] vertices, uint[] indices, Rect bounds)
    {
        _vertices = vertices;
        _indices = indices;
        Bounds = bounds;
    }

    /// <summary>
    /// Gets an empty mesh with no vertices, indices or bounds.
    /// </summary>
    internal static UiTriangleMesh Empty { get; } =
        new(Array.Empty<Point>(), Array.Empty<uint>(), Rect.Zero);

    /// <summary>
    /// Gets the triangle vertices.
    /// </summary>
    internal Point[] Vertices => _vertices;

    /// <summary>
    /// Gets the triangle indices, three per triangle.
    /// </summary>
    internal uint[] Indices => _indices;

    /// <summary>
    /// Gets the axis-aligned bounds of the vertices, or <see cref="Rect.Zero"/> when empty.
    /// </summary>
    internal Rect Bounds { get; }

    /// <summary>
    /// Determines whether the point lies inside or on the boundary of any non-degenerate triangle.
    /// </summary>
    internal bool Contains(Point point)
    {
        var vertices = _vertices;
        var indices = _indices;
        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
            var a = vertices[indices[i]];
            var b = vertices[indices[i + 1]];
            var c = vertices[indices[i + 2]];
            if (ContainsInTriangle(a, b, c, point))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns a mesh whose vertices are scaled and translated, reusing the same indices.
    /// </summary>
    internal UiTriangleMesh Transform(double scaleX, double scaleY, double translateX, double translateY)
    {
        if (_vertices.Length == 0)
            return Empty;

        var transformed = new Point[_vertices.Length];
        for (int i = 0; i < transformed.Length; i++)
        {
            var vertex = _vertices[i];
            transformed[i] = new Point(vertex.X * scaleX + translateX, vertex.Y * scaleY + translateY);
        }
        return new UiTriangleMesh(transformed, _indices);
    }

    private static bool ContainsInTriangle(Point a, Point b, Point c, Point point)
    {
        double abx = b.X - a.X;
        double aby = b.Y - a.Y;
        double acx = c.X - a.X;
        double acy = c.Y - a.Y;
        double area2 = abx * acy - aby * acx;
        double scale = Math.Abs(abx) + Math.Abs(aby) + Math.Abs(acx) + Math.Abs(acy);
        if (Math.Abs(area2) <= scale * DegenerateAreaRatio)
            return false;

        double d1 = abx * (point.Y - a.Y) - aby * (point.X - a.X);
        double d2 = (c.X - b.X) * (point.Y - b.Y) - (c.Y - b.Y) * (point.X - b.X);
        double d3 = (a.X - c.X) * (point.Y - c.Y) - (a.Y - c.Y) * (point.X - c.X);

        bool hasNegative = d1 < 0.0 || d2 < 0.0 || d3 < 0.0;
        bool hasPositive = d1 > 0.0 || d2 > 0.0 || d3 > 0.0;
        return !(hasNegative && hasPositive);
    }

    private static Rect ComputeBounds(Point[] vertices)
    {
        if (vertices.Length == 0)
            return Rect.Zero;

        double minX = vertices[0].X;
        double maxX = minX;
        double minY = vertices[0].Y;
        double maxY = minY;

        for (int i = 1; i < vertices.Length; i++)
        {
            var vertex = vertices[i];
            if (vertex.X < minX) minX = vertex.X;
            if (vertex.X > maxX) maxX = vertex.X;
            if (vertex.Y < minY) minY = vertex.Y;
            if (vertex.Y > maxY) maxY = vertex.Y;
        }

        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }
}

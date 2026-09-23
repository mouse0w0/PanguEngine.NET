using LibTessDotNet;

namespace PanguEngine.Client.UI.Drawing.Geometry;

/// <summary>
/// Builds triangle meshes for filled and stroked two-dimensional contours.
/// </summary>
internal static class UiShapeTessellator
{
    /// <summary>
    /// Triangulates the filled region of the contours using the requested fill rule.
    /// </summary>
    internal static UiTriangleMesh Fill(FlattenedContour[] contours, ShapeFillRule rule)
    {
        var polygons = new List<Point[]>(contours.Length);
        foreach (var contour in contours)
        {
            var points = CleanContour(contour.Points, closeDuplicate: true);
            if (points.Count >= 3)
                polygons.Add(points.ToArray());
        }

        if (polygons.Count == 0)
            return UiTriangleMesh.Empty;

        var windingRule = rule == ShapeFillRule.EvenOdd ? WindingRule.EvenOdd : WindingRule.NonZero;
        return Tessellate(polygons, windingRule);
    }

    /// <summary>
    /// Builds the stroked outline of the contours and triangulates its union.
    /// </summary>
    internal static UiTriangleMesh Stroke(FlattenedContour[] contours, double thickness,
        StrokeLineCap cap, StrokeLineJoin join, double miterLimit, double tolerance)
    {
        if (contours.Length == 0 || !double.IsFinite(thickness) || thickness <= 0.0)
            return UiTriangleMesh.Empty;

        var polygons = UiStrokeOutlineBuilder.Build(contours, thickness, cap, join, miterLimit, tolerance);
        if (polygons.Count == 0)
            return UiTriangleMesh.Empty;

        PrepareStrokePolygons(polygons);
        if (polygons.Count == 0)
            return UiTriangleMesh.Empty;

        return Tessellate(polygons, WindingRule.NonZero);
    }

    /// <summary>
    /// Removes consecutive duplicate points, optionally dropping a duplicate closing point.
    /// </summary>
    internal static List<Point> CleanContour(Point[] points, bool closeDuplicate)
    {
        var result = new List<Point>(points.Length);
        foreach (var point in points)
        {
            if (result.Count > 0 && result[^1] == point)
                continue;
            result.Add(point);
        }

        if (closeDuplicate && result.Count > 1 && result[0] == result[^1])
            result.RemoveAt(result.Count - 1);

        return result;
    }

    private static void PrepareStrokePolygons(List<Point[]> polygons)
    {
        for (int i = 0; i < polygons.Count; i++)
            polygons[i] = OrientCounterClockwise(CleanContour(polygons[i], closeDuplicate: true).ToArray());

        polygons.RemoveAll(polygon => polygon.Length < 3);
    }

    private static Point[] OrientCounterClockwise(Point[] polygon)
    {
        if (polygon.Length < 3)
            return polygon;

        double area = 0.0;
        for (int i = 0; i < polygon.Length; i++)
        {
            var current = polygon[i];
            var next = polygon[(i + 1) % polygon.Length];
            area += current.X * next.Y - next.X * current.Y;
        }

        if (area < 0.0)
            Array.Reverse(polygon);

        return polygon;
    }

    private static UiTriangleMesh Tessellate(List<Point[]> polygons, WindingRule windingRule)
    {
        double originX = double.PositiveInfinity;
        double originY = double.PositiveInfinity;
        foreach (var polygon in polygons)
        {
            foreach (var point in polygon)
            {
                if (point.X < originX) originX = point.X;
                if (point.Y < originY) originY = point.Y;
            }
        }

        var tess = new Tess { NoEmptyPolygons = true };
        foreach (var polygon in polygons)
        {
            var contour = new ContourVertex[polygon.Length];
            for (int i = 0; i < polygon.Length; i++)
            {
                var point = polygon[i];
                contour[i] = new ContourVertex(
                    new Vec3((float)(point.X - originX), (float)(point.Y - originY), 0.0f));
            }
            tess.AddContour(contour, ContourOrientation.Original);
        }

        tess.Tessellate(windingRule, ElementType.Polygons, 3);

        int elementCount = tess.ElementCount;
        if (elementCount == 0)
            return UiTriangleMesh.Empty;

        var vertices = new Point[tess.VertexCount];
        for (int i = 0; i < vertices.Length; i++)
        {
            var position = tess.Vertices[i].Position;
            vertices[i] = new Point((double)position.X + originX, (double)position.Y + originY);
        }

        var elements = tess.Elements;
        var indices = new List<uint>(elementCount * 3);
        for (int element = 0; element < elementCount; element++)
        {
            int i0 = elements[element * 3];
            int i1 = elements[element * 3 + 1];
            int i2 = elements[element * 3 + 2];
            if (i0 == Tess.Undef || i1 == Tess.Undef || i2 == Tess.Undef)
                continue;

            var a = vertices[i0];
            var b = vertices[i1];
            var c = vertices[i2];
            double doubledArea = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
            if (doubledArea == 0.0)
                continue;

            indices.Add((uint)i0);
            indices.Add((uint)i1);
            indices.Add((uint)i2);
        }

        if (indices.Count == 0)
            return UiTriangleMesh.Empty;

        return new UiTriangleMesh(vertices, indices.ToArray());
    }
}

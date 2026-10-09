using System.Runtime.CompilerServices;
using PanguEngine.Client.UI.Drawing.Geometry;

namespace PanguEngine.Client.UI.Rendering;

/// <summary>
/// Describes one solid pixel-aligned rectangle carrying an accumulated coverage factor.
/// </summary>
internal readonly record struct UiCoverageQuad(int X, int Y, uint Width, uint Height, float Coverage);

/// <summary>
/// Converts triangle meshes into pixel coverage rectangles without edge bands.
/// </summary>
/// <remarks>
/// Coverage is accumulated per physical pixel so shared triangle edges and overlapping
/// tessellation output remain single-blended. Geometry is scanned only inside the supplied
/// clip, and each mesh keeps a single most recent result. Color and opacity never affect
/// coverage; the cached result is anchored at integer translation so only the fractional
/// part selects the pattern.
/// </remarks>
internal sealed class UiGeometryRasterizer
{
    private readonly ConditionalWeakTable<UiTriangleMesh, CacheEntry> _cache = new();
    private readonly ConditionalWeakTable<UiTriangleMesh, ClippedMeshEntry> _clippedMeshes = new();

    internal long CellCoverageEvaluations { get; private set; }

    internal IReadOnlyList<UiCoverageQuad> Rasterize(
        UiTriangleMesh mesh,
        double scale,
        double translateX,
        double translateY,
        UiScissor clip,
        UiConvexClip? geometryClip = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (geometryClip is not null)
        {
            if (!_clippedMeshes.TryGetValue(mesh, out var clipped) ||
                clipped.Scale != scale || clipped.X != translateX || clipped.Y != translateY ||
                (!ReferenceEquals(clipped.Clip, geometryClip) && !clipped.Clip.ContentEquals(geometryClip)))
            {
                clipped = new ClippedMeshEntry(scale, translateX, translateY, geometryClip,
                    geometryClip.Apply(mesh, scale, translateX, translateY));
                _clippedMeshes.Remove(mesh);
                _clippedMeshes.Add(mesh, clipped);
            }
            return Rasterize(clipped.Mesh, 1, 0, 0, clip);
        }
        var originX = (long)Math.Floor(translateX);
        var originY = (long)Math.Floor(translateY);
        var fractionX = translateX - originX;
        var fractionY = translateY - originY;
        var relativeLeft = (long)clip.X - originX;
        var relativeTop = (long)clip.Y - originY;
        var relativeRight = relativeLeft + clip.Width;
        var relativeBottom = relativeTop + clip.Height;
        if (relativeRight <= relativeLeft || relativeBottom <= relativeTop)
            return [];

        if (_cache.TryGetValue(mesh, out var entry) &&
            entry.Matches(scale, fractionX, fractionY, relativeLeft, relativeTop, relativeRight, relativeBottom))
        {
            return entry.OriginX == originX && entry.OriginY == originY
                ? entry.Quads
                : Offset(entry.Quads, (int)(originX - entry.OriginX), (int)(originY - entry.OriginY));
        }

        var clipLeft = clip.X;
        var clipTop = clip.Y;
        var clipRight = clipLeft + (int)clip.Width;
        var clipBottom = clipTop + (int)clip.Height;
        var quads = Compute(mesh, scale, translateX, translateY, clipLeft, clipTop, clipRight, clipBottom);
        _cache.Remove(mesh);
        _cache.Add(
            mesh,
            new CacheEntry(
                scale,
                fractionX,
                fractionY,
                relativeLeft,
                relativeTop,
                relativeRight,
                relativeBottom,
                originX,
                originY,
                quads));
        return quads;
    }

    private sealed record ClippedMeshEntry(double Scale, double X, double Y, UiConvexClip Clip, UiTriangleMesh Mesh);

    private UiCoverageQuad[] Compute(
        UiTriangleMesh mesh,
        double scale,
        double translateX,
        double translateY,
        int clipLeft,
        int clipTop,
        int clipRight,
        int clipBottom)
    {
        var vertices = mesh.Vertices;
        var indices = mesh.Indices;
        var rows = new SortedDictionary<int, CoverageRow>();
        Span<double> polygonAx = stackalloc double[8];
        Span<double> polygonAy = stackalloc double[8];
        Span<double> polygonBx = stackalloc double[8];
        Span<double> polygonBy = stackalloc double[8];

        for (var index = 0; index + 2 < indices.Length; index += 3)
        {
            var ax = TransformCoordinate(vertices[indices[index]].X, scale, translateX);
            var ay = TransformCoordinate(vertices[indices[index]].Y, scale, translateY);
            var bx = TransformCoordinate(vertices[indices[index + 1]].X, scale, translateX);
            var by = TransformCoordinate(vertices[indices[index + 1]].Y, scale, translateY);
            var cx = TransformCoordinate(vertices[indices[index + 2]].X, scale, translateX);
            var cy = TransformCoordinate(vertices[indices[index + 2]].Y, scale, translateY);

            var minY = Math.Min(ay, Math.Min(by, cy));
            var maxY = Math.Max(ay, Math.Max(by, cy));

            var scanTop = Math.Max(minY, (double)clipTop);
            var scanBottom = Math.Min(maxY, (double)clipBottom);
            if (scanBottom <= scanTop)
                continue;

            var startY = Math.Max(clipTop, (int)Math.Floor(scanTop));
            var endY = Math.Min(clipBottom - 1, (int)Math.Ceiling(scanBottom) - 1);

            for (var y = startY; y <= endY; y++)
            {
                polygonAx[0] = ax;
                polygonAy[0] = ay;
                polygonAx[1] = bx;
                polygonAy[1] = by;
                polygonAx[2] = cx;
                polygonAy[2] = cy;
                var rowCount = 3;
                rowCount = ClipEdge(polygonAx, polygonAy, polygonBx, polygonBy, rowCount, y, 2);
                rowCount = ClipEdge(polygonBx, polygonBy, polygonAx, polygonAy, rowCount, y + 1, 3);
                if (rowCount < 3)
                    continue;

                var rowMinX = polygonAx[0];
                var rowMaxX = polygonAx[0];
                for (var vertex = 1; vertex < rowCount; vertex++)
                {
                    var value = polygonAx[vertex];
                    if (value < rowMinX)
                        rowMinX = value;
                    if (value > rowMaxX)
                        rowMaxX = value;
                }

                var scanLeft = Math.Max(rowMinX, (double)clipLeft);
                var scanRight = Math.Min(rowMaxX, (double)clipRight);
                if (scanRight <= scanLeft)
                    continue;

                var startX = Math.Max(clipLeft, (int)Math.Floor(scanLeft));
                var endX = Math.Min(clipRight - 1, (int)Math.Ceiling(scanRight) - 1);
                var fullStart = startX;
                var fullEnd = startX;
                if (minY <= y && maxY >= y + 1d)
                {
                    var topLeft = double.PositiveInfinity;
                    var topRight = double.NegativeInfinity;
                    var bottomLeft = double.PositiveInfinity;
                    var bottomRight = double.NegativeInfinity;
                    SliceTriangle(ax, ay, bx, by, cx, cy, y, ref topLeft, ref topRight);
                    SliceTriangle(ax, ay, bx, by, cx, cy, y + 1d, ref bottomLeft, ref bottomRight);
                    fullStart = (int)Math.Ceiling(Math.Clamp(Math.Max(topLeft, bottomLeft), startX, endX + 1d));
                    fullEnd = (int)Math.Floor(Math.Clamp(Math.Min(topRight, bottomRight), startX, endX + 1d));
                    if (fullEnd < fullStart)
                        fullStart = fullEnd = startX;
                }

                if (!rows.TryGetValue(y, out var row))
                    rows.Add(y, row = new CoverageRow());
                if (fullEnd > fullStart)
                    row.AddSpan(fullStart, fullEnd);
                for (var x = startX; x < fullStart; x++)
                    AddBoundaryCell(row, ax, ay, bx, by, cx, cy, x, y,
                        polygonAx, polygonAy, polygonBx, polygonBy);
                for (var x = fullEnd; x <= endX; x++)
                    AddBoundaryCell(row, ax, ay, bx, by, cx, cy, x, y,
                        polygonAx, polygonAy, polygonBx, polygonBy);
            }
        }

        var quads = new List<UiCoverageQuad>();
        foreach (var (y, row) in rows)
            row.Emit(y, quads);
        return MergeRows(quads);
    }

    private static UiCoverageQuad[] MergeRows(List<UiCoverageQuad> rows)
    {
        var merged = new List<UiCoverageQuad>();
        var previous = new Dictionary<(int X, uint Width, float Coverage), int>();
        var current = new Dictionary<(int X, uint Width, float Coverage), int>();
        var previousY = int.MinValue;
        var position = 0;
        while (position < rows.Count)
        {
            var y = rows[position].Y;
            current.Clear();
            do
            {
                var quad = rows[position++];
                var key = (quad.X, quad.Width, quad.Coverage);
                int index;
                if ((long)previousY + 1 == y && previous.TryGetValue(key, out index))
                    merged[index] = merged[index] with { Height = merged[index].Height + quad.Height };
                else
                {
                    index = merged.Count;
                    merged.Add(quad);
                }
                current.Add(key, index);
            } while (position < rows.Count && rows[position].Y == y);
            (previous, current) = (current, previous);
            previousY = y;
        }
        return [.. merged];
    }

    private void AddBoundaryCell(
        CoverageRow row, double ax, double ay, double bx, double by, double cx, double cy, int x, int y,
        Span<double> polygonAx, Span<double> polygonAy, Span<double> polygonBx, Span<double> polygonBy)
    {
        CellCoverageEvaluations++;
        var area = CellCoverage(ax, ay, bx, by, cx, cy, x, y, polygonAx, polygonAy, polygonBx, polygonBy);
        if (area > 0)
            row.AddCell(x, area);
    }

    private static void SliceTriangle(
        double ax, double ay, double bx, double by, double cx, double cy, double y,
        ref double left, ref double right)
    {
        SliceEdge(ax, ay, bx, by, y, ref left, ref right);
        SliceEdge(bx, by, cx, cy, y, ref left, ref right);
        SliceEdge(cx, cy, ax, ay, y, ref left, ref right);
    }

    private static void SliceEdge(double ax, double ay, double bx, double by, double y, ref double left, ref double right)
    {
        if (y < Math.Min(ay, by) || y > Math.Max(ay, by))
            return;
        if (ay == by)
        {
            left = Math.Min(left, Math.Min(ax, bx));
            right = Math.Max(right, Math.Max(ax, bx));
            return;
        }
        var x = ax + (bx - ax) * ((y - ay) / (by - ay));
        left = Math.Min(left, x);
        right = Math.Max(right, x);
    }

    private static double CellCoverage(
        double ax,
        double ay,
        double bx,
        double by,
        double cx,
        double cy,
        int cellX,
        int cellY,
        Span<double> polygonAx,
        Span<double> polygonAy,
        Span<double> polygonBx,
        Span<double> polygonBy)
    {
        polygonAx[0] = ax;
        polygonAy[0] = ay;
        polygonAx[1] = bx;
        polygonAy[1] = by;
        polygonAx[2] = cx;
        polygonAy[2] = cy;
        var count = 3;

        var left = (double)cellX;
        var top = (double)cellY;
        var right = left + 1;
        var bottom = top + 1;

        count = ClipEdge(polygonAx, polygonAy, polygonBx, polygonBy, count, left, 0);
        count = ClipEdge(polygonBx, polygonBy, polygonAx, polygonAy, count, right, 1);
        count = ClipEdge(polygonAx, polygonAy, polygonBx, polygonBy, count, top, 2);
        count = ClipEdge(polygonBx, polygonBy, polygonAx, polygonAy, count, bottom, 3);
        if (count < 3)
            return 0;

        var twiceArea = 0.0;
        for (var index = 0; index < count; index++)
        {
            var next = index + 1 == count ? 0 : index + 1;
            var currentX = polygonAx[index] - left;
            var currentY = polygonAy[index] - top;
            var nextX = polygonAx[next] - left;
            var nextY = polygonAy[next] - top;
            twiceArea += currentX * nextY - nextX * currentY;
        }

        var area = Math.Abs(twiceArea) * 0.5;
        if (area >= 1 - 1e-9)
            return 1;
        return area <= 1e-12 ? 0 : area;
    }

    private static int ClipEdge(
        ReadOnlySpan<double> sourceX,
        ReadOnlySpan<double> sourceY,
        Span<double> targetX,
        Span<double> targetY,
        int count,
        double value,
        int edge)
    {
        var outCount = 0;
        for (var index = 0; index < count; index++)
        {
            var next = index + 1 == count ? 0 : index + 1;
            var currentX = sourceX[index];
            var currentY = sourceY[index];
            var nextX = sourceX[next];
            var nextY = sourceY[next];
            var currentDistance = EdgeDistance(edge, currentX, currentY, value);
            var nextDistance = EdgeDistance(edge, nextX, nextY, value);
            var currentInside = currentDistance >= 0;
            var nextInside = nextDistance >= 0;
            if (nextInside)
            {
                if (!currentInside)
                {
                    var entering = currentDistance / (currentDistance - nextDistance);
                    targetX[outCount] = currentX + (nextX - currentX) * entering;
                    targetY[outCount] = currentY + (nextY - currentY) * entering;
                    outCount++;
                }

                targetX[outCount] = nextX;
                targetY[outCount] = nextY;
                outCount++;
            }
            else if (currentInside)
            {
                var leaving = currentDistance / (currentDistance - nextDistance);
                targetX[outCount] = currentX + (nextX - currentX) * leaving;
                targetY[outCount] = currentY + (nextY - currentY) * leaving;
                outCount++;
            }
        }

        return outCount;
    }

    private static double EdgeDistance(int edge, double x, double y, double value) => edge switch
    {
        0 => x - value,
        1 => value - x,
        2 => y - value,
        _ => value - y
    };

    private static double TransformCoordinate(double value, double scale, double translate)
    {
        var result = value * scale + translate;
        if (!double.IsFinite(result))
            throw new InvalidOperationException("UI drawing produced a non-finite geometry coordinate.");
        return result;
    }

    private static UiCoverageQuad[] Offset(UiCoverageQuad[] quads, int offsetX, int offsetY)
    {
        var result = new UiCoverageQuad[quads.Length];
        for (var index = 0; index < quads.Length; index++)
        {
            var quad = quads[index];
            result[index] = new UiCoverageQuad(
                quad.X + offsetX,
                quad.Y + offsetY,
                quad.Width,
                quad.Height,
                quad.Coverage);
        }

        return result;
    }

    private readonly record struct SpanEvent(int X, int Delta);

    private sealed class CoverageRow
    {
        private readonly List<SpanEvent> _events = [];
        private readonly Dictionary<int, double> _cells = [];

        internal void AddSpan(int first, int end)
        {
            _events.Add(new SpanEvent(first, 1));
            _events.Add(new SpanEvent(end, -1));
        }

        internal void AddCell(int x, double coverage)
        {
            if (_cells.TryGetValue(x, out var previous))
                _cells[x] = Math.Min(1, previous + coverage);
            else
            {
                _cells.Add(x, coverage);
                _events.Add(new SpanEvent(x, 0));
                _events.Add(new SpanEvent(x + 1, 0));
            }
        }

        internal void Emit(int y, List<UiCoverageQuad> quads)
        {
            _events.Sort(static (a, b) => a.X.CompareTo(b.X));
            var position = 0;
            var depth = 0;
            while (position < _events.Count)
            {
                var x = _events[position].X;
                do
                {
                    depth += _events[position++].Delta;
                } while (position < _events.Count && _events[position].X == x);
                if (position == _events.Count)
                    break;
                _cells.TryGetValue(x, out var partial);
                var coverage = Math.Min(1, depth + partial);
                if (coverage <= 0)
                    continue;
                if (coverage >= 1 - 1e-9)
                    coverage = 1;
                var width = (uint)(_events[position].X - x);
                var alpha = (float)coverage;
                if (quads.Count > 0 && quads[^1] is var last && last.Y == y &&
                    last.X + (long)last.Width == x && last.Coverage == alpha)
                    quads[^1] = last with { Width = last.Width + width };
                else
                    quads.Add(new UiCoverageQuad(x, y, width, 1, alpha));
            }
        }
    }

    private sealed class CacheEntry
    {
        internal CacheEntry(
            double scale,
            double fractionX,
            double fractionY,
            long relativeLeft,
            long relativeTop,
            long relativeRight,
            long relativeBottom,
            long originX,
            long originY,
            UiCoverageQuad[] quads)
        {
            Scale = scale;
            FractionX = fractionX;
            FractionY = fractionY;
            RelativeLeft = relativeLeft;
            RelativeTop = relativeTop;
            RelativeRight = relativeRight;
            RelativeBottom = relativeBottom;
            OriginX = originX;
            OriginY = originY;
            Quads = quads;
        }

        internal double Scale { get; }

        internal double FractionX { get; }

        internal double FractionY { get; }

        internal long RelativeLeft { get; }

        internal long RelativeTop { get; }

        internal long RelativeRight { get; }

        internal long RelativeBottom { get; }

        internal long OriginX { get; }

        internal long OriginY { get; }

        internal UiCoverageQuad[] Quads { get; }

        internal bool Matches(
            double scale,
            double fractionX,
            double fractionY,
            long relativeLeft,
            long relativeTop,
            long relativeRight,
            long relativeBottom) =>
            Scale == scale &&
            FractionX == fractionX &&
            FractionY == fractionY &&
            RelativeLeft == relativeLeft &&
            RelativeTop == relativeTop &&
            RelativeRight == relativeRight &&
            RelativeBottom == relativeBottom;
    }
}

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

    internal int CacheHits { get; private set; }

    internal int CacheMisses { get; private set; }

    internal IReadOnlyList<UiCoverageQuad> Rasterize(
        UiTriangleMesh mesh,
        double scale,
        double translateX,
        double translateY,
        UiScissor clip)
    {
        ArgumentNullException.ThrowIfNull(mesh);
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
            CacheHits++;
            return entry.OriginX == originX && entry.OriginY == originY
                ? entry.Quads
                : Offset(entry.Quads, (int)(originX - entry.OriginX), (int)(originY - entry.OriginY));
        }

        CacheMisses++;
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

    private static UiCoverageQuad[] Compute(
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
        var cells = new Dictionary<long, double>();
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

                for (var x = startX; x <= endX; x++)
                {
                    var area = CellCoverage(
                        ax,
                        ay,
                        bx,
                        by,
                        cx,
                        cy,
                        x,
                        y,
                        polygonAx,
                        polygonAy,
                        polygonBx,
                        polygonBy);
                    if (area <= 0)
                        continue;

                    var key = CellKey(x, y);
                    cells.TryGetValue(key, out var existing);
                    var total = existing + area;
                    cells[key] = total > 1 ? 1 : total;
                }
            }
        }

        if (cells.Count == 0)
            return [];

        var items = new List<Cell>(cells.Count);
        foreach (var pair in cells)
        {
            var coverage = pair.Value;
            if (coverage <= 0)
                continue;
            if (coverage >= 1 - 1e-9)
                coverage = 1;
            items.Add(new Cell((int)(pair.Key & 0xffffffff), (int)(pair.Key >> 32), coverage));
        }

        if (items.Count == 0)
            return [];

        items.Sort(static (first, second) =>
            first.Y != second.Y ? first.Y.CompareTo(second.Y) : first.X.CompareTo(second.X));

        var quads = new List<UiCoverageQuad>();
        var position = 0;
        while (position < items.Count)
        {
            var row = items[position].Y;
            var startX = items[position].X;
            var endX = startX;
            var coverage = items[position].Coverage;
            position++;
            while (position < items.Count &&
                   items[position].Y == row &&
                   items[position].X == endX + 1 &&
                   items[position].Coverage == coverage)
            {
                endX = items[position].X;
                position++;
            }

            quads.Add(new UiCoverageQuad(
                startX,
                row,
                (uint)(endX - startX + 1),
                1,
                (float)coverage));
        }

        return [.. quads];
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

    private static long CellKey(int x, int y) => ((long)y << 32) | (uint)x;

    private readonly record struct Cell(int X, int Y, double Coverage);

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

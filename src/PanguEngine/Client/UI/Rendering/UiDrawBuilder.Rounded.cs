using System.Numerics;
using System.Runtime.InteropServices;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Drawing.Geometry;

namespace PanguEngine.Client.UI.Rendering;

internal sealed partial class UiDrawBuilder
{
    private readonly List<UiGpuDrawData> _drawData = [];
    private readonly List<UiGpuClipData> _clipData = [];
    private readonly Dictionary<ClipKey, uint> _clipIndices = [];
    private readonly Dictionary<uint, uint> _drawIndices = [];
    private ClipState? _activeClip;
    private Rect? _imagePartitionBounds;

    internal ReadOnlySpan<UiGpuDrawData> DrawData => CollectionsMarshal.AsSpan(_drawData);
    internal ReadOnlySpan<UiGpuClipData> ClipData => CollectionsMarshal.AsSpan(_clipData);

    private void ResetGpuData()
    {
        _drawData.Clear();
        _clipData.Clear();
        _clipIndices.Clear();
        _drawIndices.Clear();
        _drawData.Add(default);
        _clipData.Add(default);
    }

    private static DrawingState PushClip(DrawingState state, UiRoundedClipGeometry geometry, bool snapImageBounds = false)
    {
        var clip = new ClipState(geometry, state.Scale, state.X, state.Y, state.ClipGeometry);
        var bounds = clip.Outer.Bounds;
        var scissor = state.Clip is { } old ? IntersectBounds(old, bounds) : bounds;
        return state with
        {
            Clip = scissor,
            ClipGeometry = clip,
            ImagePartitionBounds = snapImageBounds ? bounds : state.ImagePartitionBounds
        };
    }

    private uint GetClipIndex(ClipState? state, Rect visible) =>
        state is null ? 0 : GetClipIndexCore(state, Expand(visible, 1));

    private uint GetClipIndexCore(ClipState state, Rect expanded)
    {
        var parent = state.Parent is { } ancestor ? GetClipIndexCore(ancestor, expanded) : 0;
        if (!Contains(state.Outer, expanded))
            parent = AddClip(state.Outer, parent);
        if (state.Inner is { } inner && inner.Bounds.Width > 0 && inner.Bounds.Height > 0 &&
            IntersectBounds(inner.Bounds, expanded) is { Width: > 0, Height: > 0 })
            parent = AddClip(inner, parent, exclude: true);
        if (state.Constraint is { } constraint && !Contains(constraint, expanded))
            parent = AddClip(constraint, parent);
        return parent;
    }

    private uint AddClip(UiRoundedRectangle shape, uint parent = 0, bool exclude = false)
    {
        var key = new ClipKey(shape, parent, exclude);
        if (_clipIndices.TryGetValue(key, out var cached))
            return cached;
        var data = new UiGpuClipData(shape, parent, exclude);
        for (var index = parent; index != 0; index = _clipData[(int)index].ParentIndex)
        {
            var existing = _clipData[(int)index];
            if (existing.Bounds == data.Bounds && existing.RadiusX == data.RadiusX &&
                existing.RadiusY == data.RadiusY && existing.Exclude == data.Exclude)
            {
                _clipIndices.Add(key, parent);
                return parent;
            }
        }
        var result = checked((uint)_clipData.Count);
        _clipData.Add(data);
        _clipIndices.Add(key, result);
        return result;
    }

    private uint GetDrawDataIndex(uint clipIndex)
    {
        if (clipIndex == 0)
            return 0;
        if (_drawIndices.TryGetValue(clipIndex, out var cached))
            return cached;
        var index = AddDrawData(new UiGpuDrawData(clipIndex));
        _drawIndices.Add(clipIndex, index);
        return index;
    }

    private uint AddDrawData(UiGpuDrawData data)
    {
        var index = checked((uint)_drawData.Count);
        if (index > UiVertex.MaxDrawDataIndex)
            throw new InvalidOperationException("UI drawing exceeded the packed draw data index range.");
        _drawData.Add(data);
        return index;
    }

    private void AppendRoundedFill(UiFillRoundedRectangleCommand command, DrawingState state,
        UiScissor scissor, bool convertSrgbToLinear)
    {
        var clipped = PushClip(state, command.Geometry);
        var outer = clipped.ClipGeometry!.Outer.Bounds;
        if (outer.Width == 0 || outer.Height == 0)
            return;
        var bounds = Expand(outer, 1);
        var physical = new PhysicalBounds(bounds.X, bounds.Y, bounds.X + bounds.Width, bounds.Y + bounds.Height);
        if (!Intersects(physical, scissor))
            return;
        _activeClip = clipped.ClipGeometry;
        var color = command.Color;
        AppendGeometry(physical,
            scissor, UiMaterialKind.Solid, 0,
            ToColorChannel(color.R, convertSrgbToLinear),
            ToColorChannel(color.G, convertSrgbToLinear),
            ToColorChannel(color.B, convertSrgbToLinear),
            (float)(color.A / 255.0 * state.Opacity), 0, 0, 0, 0, 0, 0);
    }

    private void AppendRoundedDecoration(UiDrawRoundedDecorationCommand command, DrawingState state,
        UiScissor scissor, bool convertSrgbToLinear)
    {
        var outer = TransformShape(command.Outer, state.Scale, state.X, state.Y);
        if (outer.Bounds.Width == 0 || outer.Bounds.Height == 0)
            return;
        var expanded = Expand(outer.Bounds, 1);
        var visible = IntersectBounds(expanded, new Rect(scissor.X, scissor.Y, scissor.Width, scissor.Height));
        if (visible.Width == 0 || visible.Height == 0)
            return;
        var inner = TransformShape(command.Inner, state.Scale, state.X, state.Y);
        var hasInner = inner.Bounds.Width > 0 && inner.Bounds.Height > 0;
        var hasBackground = hasInner && command.Background.A != 0;
        var hasBorder = command.Border.A != 0 && inner != outer;
        if (!hasBackground && !hasBorder)
            return;
        var dataIndex = AddDrawData(new UiGpuDrawData(
            GetClipIndex(state.ClipGeometry, visible), AddClip(outer), hasInner ? AddClip(inner) : 0,
            hasBackground ? ConvertColor(command.Background, state.Opacity, convertSrgbToLinear) : default,
            hasBorder ? ConvertColor(command.Border, state.Opacity, convertSrgbToLinear) : default, decoration: 1));

        if (!hasBackground && hasInner && TryGetSafeHole(inner, expanded, out var hole))
            AppendAnalyticRing(expanded, hole, scissor, dataIndex);
        else
            AppendAnalyticQuad(expanded, scissor, dataIndex);
    }

    private void AppendRoundedShape(UiDrawRoundedShapeCommand command, DrawingState state,
        UiScissor scissor, bool convertSrgbToLinear)
    {
        var centerline = TransformShape(command.Centerline, state.Scale, state.X, state.Y);
        var halfWidth = command.Thickness * state.Scale / 2;
        var expanded = Expand(centerline.Bounds, halfWidth + 1);
        var visible = IntersectBounds(expanded, new Rect(scissor.X, scissor.Y, scissor.Width, scissor.Height));
        if (visible.Width == 0 || visible.Height == 0)
            return;
        var mode = centerline.TopLeft == Point.Zero
            ? command.Join switch
            {
                StrokeLineJoin.Bevel => 4u,
                StrokeLineJoin.Round => 5u,
                _ => 3u
            }
            : 2u;
        var dataIndex = AddDrawData(new UiGpuDrawData(
            GetClipIndex(state.ClipGeometry, visible), AddClip(centerline),
            innerIndex: BitConverter.SingleToUInt32Bits((float)halfWidth),
            background: ConvertColor(command.Fill, state.Opacity, convertSrgbToLinear),
            border: ConvertColor(command.Stroke, state.Opacity, convertSrgbToLinear), decoration: mode));
        if (command.Fill.A == 0 && TryGetSafeHole(centerline, expanded, out var hole, halfWidth + 1))
            AppendAnalyticRing(expanded, hole, scissor, dataIndex);
        else
            AppendAnalyticQuad(expanded, scissor, dataIndex);
    }

    private void AppendAnalyticRing(Rect outer, Rect hole, UiScissor scissor, uint dataIndex)
    {
        AppendAnalyticQuad(new Rect(outer.X, outer.Y, outer.Width, hole.Y - outer.Y), scissor, dataIndex);
        AppendAnalyticQuad(new Rect(outer.X, hole.Y + hole.Height, outer.Width,
            outer.Y + outer.Height - hole.Y - hole.Height), scissor, dataIndex);
        AppendAnalyticQuad(new Rect(outer.X, hole.Y, hole.X - outer.X, hole.Height), scissor, dataIndex);
        AppendAnalyticQuad(new Rect(hole.X + hole.Width, hole.Y,
            outer.X + outer.Width - hole.X - hole.Width, hole.Height), scissor, dataIndex);
    }

    private void AppendAnalyticQuad(Rect bounds, UiScissor scissor, uint dataIndex)
    {
        if (bounds.Width == 0 || bounds.Height == 0)
            return;
        var physical = new PhysicalBounds(bounds.X, bounds.Y, bounds.X + bounds.Width, bounds.Y + bounds.Height);
        if (!Intersects(physical, scissor))
            return;
        _legacyQuadCount++;
        AppendQuadRaw(physical,
            scissor, UiMaterialKind.Solid, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, drawDataIndex: dataIndex);
    }

    private static Vector4 ConvertColor(Color color, double opacity, bool convertSrgbToLinear) => new(
        ToColorChannel(color.R, convertSrgbToLinear), ToColorChannel(color.G, convertSrgbToLinear),
        ToColorChannel(color.B, convertSrgbToLinear), (float)(color.A / 255.0 * opacity));

    private static bool TryGetSafeHole(UiRoundedRectangle inner, Rect outer, out Rect hole, double inset = 1)
    {
        var bounds = inner.Bounds;
        var left = bounds.X + Math.Max(inner.TopLeft.X, inner.BottomLeft.X) + inset;
        var right = bounds.X + bounds.Width - Math.Max(inner.TopRight.X, inner.BottomRight.X) - inset;
        var top = bounds.Y + Math.Max(inner.TopLeft.Y, inner.TopRight.Y) + inset;
        var bottom = bounds.Y + bounds.Height - Math.Max(inner.BottomLeft.Y, inner.BottomRight.Y) - inset;
        hole = IntersectBounds(new Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top)), outer);
        return hole.Width * hole.Height > outer.Width * outer.Height / 2;
    }

    private static Rect IntersectBounds(Rect a, Rect b)
    {
        var x = Math.Max(a.X, b.X);
        var y = Math.Max(a.Y, b.Y);
        return new Rect(x, y, Math.Max(0, Math.Min(a.X + a.Width, b.X + b.Width) - x),
            Math.Max(0, Math.Min(a.Y + a.Height, b.Y + b.Height) - y));
    }

    private static Rect Expand(Rect bounds, double amount) => new(
        bounds.X - amount, bounds.Y - amount, bounds.Width + 2 * amount, bounds.Height + 2 * amount);

    private static bool Contains(UiRoundedRectangle shape, Rect bounds) =>
        shape.Contains(new Point(bounds.X, bounds.Y)) &&
        shape.Contains(new Point(bounds.X + bounds.Width, bounds.Y)) &&
        shape.Contains(new Point(bounds.X + bounds.Width, bounds.Y + bounds.Height)) &&
        shape.Contains(new Point(bounds.X, bounds.Y + bounds.Height));

    private static bool TryGetImageBounds(Rect bounds, uint framebufferWidth, uint framebufferHeight,
        DrawingState state, UiScissor scissor, out PhysicalBounds physicalBounds)
    {
        if (state.ImagePartitionBounds is not { } partition)
            return TryGetPhysicalBounds(bounds, framebufferWidth, framebufferHeight, state, out physicalBounds) &&
                Intersects(physicalBounds, scissor);

        var transformed = Transform(bounds, state);
        physicalBounds = new PhysicalBounds(transformed.X, transformed.Y,
            transformed.X + transformed.Width, transformed.Y + transformed.Height);
        var output = SnapImageBounds(physicalBounds, partition);
        return output.Right > output.Left && output.Bottom > output.Top && Intersects(output, scissor);
    }

    private void AppendQuad(
        PhysicalBounds bounds, UiScissor scissor, UiMaterialKind materialKind, uint textureIndex,
        float r, float g, float b, float a, float u0, float v0,
        float clampMinU, float clampMinV, float clampMaxU, float clampMaxV, float u1 = 0, float v1 = 0)
    {
        var output = bounds;
        if (_imagePartitionBounds is { } partition &&
            materialKind is UiMaterialKind.ImageNearest or UiMaterialKind.ImageLinear)
            output = SnapImageBounds(bounds, partition);
        if (output.Right <= output.Left || output.Bottom <= output.Top)
            return;
        var rectangle = new Rect(output.Left, output.Top, output.Right - output.Left, output.Bottom - output.Top);
        var visible = IntersectBounds(rectangle, new Rect(scissor.X, scissor.Y, scissor.Width, scissor.Height));
        if (visible.Width == 0 || visible.Height == 0)
            return;
        var dataIndex = GetDrawDataIndex(GetClipIndex(_activeClip, visible));
        AppendQuadRaw(output, scissor, materialKind, textureIndex, r, g, b, a,
            Map(output.Left, bounds.Left, bounds.Right, u0, u1),
            Map(output.Top, bounds.Top, bounds.Bottom, v0, v1),
            clampMinU, clampMinV, clampMaxU, clampMaxV,
            Map(output.Right, bounds.Left, bounds.Right, u0, u1),
            Map(output.Bottom, bounds.Top, bounds.Bottom, v0, v1), dataIndex);
    }

    private static PhysicalBounds SnapImageBounds(PhysicalBounds bounds, Rect partition) => new(
        SnapPartition(bounds.Left, partition.X, partition.X + partition.Width),
        SnapPartition(bounds.Top, partition.Y, partition.Y + partition.Height),
        SnapPartition(bounds.Right, partition.X, partition.X + partition.Width),
        SnapPartition(bounds.Bottom, partition.Y, partition.Y + partition.Height));

    private static double SnapPartition(double value, double first, double last) =>
        value <= first ? Math.Floor(value) : value >= last ? Math.Ceiling(value) : Math.Round(value, MidpointRounding.AwayFromZero);

    private static float Map(double value, double first, double last, float start, float end) =>
        (float)(start + (value - first) / (last - first) * (end - start));

    private readonly record struct ClipKey(UiRoundedRectangle Shape, uint Parent, bool Exclude);

    private sealed class ClipState
    {
        private readonly UiRoundedClipGeometry _geometry;
        private readonly double _scale;
        private readonly double _x;
        private readonly double _y;
        private UiConvexClip? _cpuClip;

        internal ClipState(UiRoundedClipGeometry geometry, double scale, double x, double y,
            ClipState? parent)
        {
            _geometry = geometry;
            _scale = scale;
            _x = x;
            _y = y;
            Parent = parent;
            Outer = TransformShape(geometry.Outer, scale, x, y);
            Inner = geometry.Inner is { } inner ? TransformShape(inner, scale, x, y) : null;
            Constraint = geometry.Constraint is { } constraint ? TransformShape(constraint, scale, x, y) : null;
        }

        internal ClipState? Parent { get; }
        internal UiRoundedRectangle Outer { get; }
        internal UiRoundedRectangle? Inner { get; }
        internal UiRoundedRectangle? Constraint { get; }

        internal bool Contains(Rect bounds)
        {
            for (ClipState? node = this; node is not null; node = node.Parent)
            {
                if (!UiDrawBuilder.Contains(node.Outer, bounds) ||
                    node.Constraint is { } constraint && !UiDrawBuilder.Contains(constraint, bounds))
                    return false;
                if (node.Inner is { } inner &&
                    IntersectBounds(inner.Bounds, bounds) is { Width: > 0, Height: > 0 })
                    return false;
            }
            return true;
        }

        internal UiConvexClip GetCpuClip() =>
            _cpuClip ??= _geometry.GetPhysicalClip(_scale, _x, _y, Parent?.GetCpuClip());
    }

    private static UiRoundedRectangle TransformShape(UiRoundedRectangle shape, double scale, double x, double y)
    {
        var bounds = Transform(shape.Bounds, new DrawingState(x, y, scale, null, 1));
        return new UiRoundedRectangle(bounds,
            new Point(shape.TopLeft.X * scale, shape.TopLeft.Y * scale),
            new Point(shape.TopRight.X * scale, shape.TopRight.Y * scale),
            new Point(shape.BottomRight.X * scale, shape.BottomRight.Y * scale),
            new Point(shape.BottomLeft.X * scale, shape.BottomLeft.Y * scale));
    }
}

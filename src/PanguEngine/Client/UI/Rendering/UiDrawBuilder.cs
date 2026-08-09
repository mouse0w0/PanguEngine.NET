using System.Runtime.InteropServices;
using PanguEngine.Graphics;

namespace PanguEngine.Client.UI.Rendering;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct UiVertex(
    float x,
    float y,
    float r,
    float g,
    float b,
    float a)
{
    internal const uint SizeInBytes = 24;

    internal static readonly VertexInputDescription VertexInput = new(
        [new VertexBufferLayoutDescription(0, SizeInBytes)],
        [
            new VertexAttributeDescription(0, 0, VertexAttributeFormat.Float32x2, 0),
            new VertexAttributeDescription(1, 0, VertexAttributeFormat.Float32x4, 8)
        ]);

    internal readonly float X = x;
    internal readonly float Y = y;
    internal readonly float R = r;
    internal readonly float G = g;
    internal readonly float B = b;
    internal readonly float A = a;
}

internal readonly record struct UiScissor(int X, int Y, uint Width, uint Height);

internal readonly record struct UiBatch(UiScissor Scissor, uint FirstIndex, uint IndexCount);

internal sealed class UiDrawBuilder
{
    private readonly List<UiVertex> _vertices = [];
    private readonly List<uint> _indices = [];
    private readonly List<UiBatch> _batches = [];

    internal ReadOnlySpan<UiVertex> Vertices => CollectionsMarshal.AsSpan(_vertices);
    internal ReadOnlySpan<uint> Indices => CollectionsMarshal.AsSpan(_indices);
    internal ReadOnlySpan<UiBatch> Batches => CollectionsMarshal.AsSpan(_batches);
    internal int RectangleCount => _vertices.Count / 4;

    internal void Build(
        UiDrawCommandList commands,
        uint framebufferWidth,
        uint framebufferHeight,
        double uiScale,
        bool convertSrgbToLinear)
    {
        ArgumentNullException.ThrowIfNull(commands);
        if (!double.IsFinite(uiScale) || uiScale <= 0)
            throw new ArgumentOutOfRangeException(nameof(uiScale), "UI scale must be finite and greater than zero.");

        _vertices.Clear();
        _indices.Clear();
        _batches.Clear();
        if (framebufferWidth == 0 || framebufferHeight == 0)
            return;

        foreach (var command in commands)
        {
            if (command is not UiFillRectangleCommand rectangle)
                throw new NotSupportedException($"UI draw command '{command.GetType().Name}' is not supported.");
            AppendRectangle(rectangle, framebufferWidth, framebufferHeight, uiScale, convertSrgbToLinear);
        }
    }

    internal static int GrowCapacity(int currentCapacity, int requiredCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(currentCapacity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requiredCapacity);
        if (currentCapacity >= requiredCapacity)
            return currentCapacity;

        var capacity = Math.Max(currentCapacity, 1);
        while (capacity < requiredCapacity)
        {
            if (capacity > int.MaxValue / 2)
                return requiredCapacity;
            capacity = checked(capacity * 2);
        }
        return capacity;
    }

    private void AppendRectangle(
        UiFillRectangleCommand command,
        uint framebufferWidth,
        uint framebufferHeight,
        double uiScale,
        bool convertSrgbToLinear)
    {
        if (!TryGetPhysicalBounds(command.Bounds, framebufferWidth, framebufferHeight, uiScale, out var bounds) ||
            !TryGetScissor(command.Clip, framebufferWidth, framebufferHeight, uiScale, out var scissor) ||
            !Intersects(bounds, scissor))
        {
            return;
        }

        var color = command.Color;
        var r = ToColorChannel(color.R, convertSrgbToLinear);
        var g = ToColorChannel(color.G, convertSrgbToLinear);
        var b = ToColorChannel(color.B, convertSrgbToLinear);
        var a = (float)(color.A / 255.0 * command.Opacity);
        var vertexBase = checked((uint)_vertices.Count);
        _vertices.Add(new UiVertex((float)bounds.Left, (float)bounds.Top, r, g, b, a));
        _vertices.Add(new UiVertex((float)bounds.Right, (float)bounds.Top, r, g, b, a));
        _vertices.Add(new UiVertex((float)bounds.Right, (float)bounds.Bottom, r, g, b, a));
        _vertices.Add(new UiVertex((float)bounds.Left, (float)bounds.Bottom, r, g, b, a));

        var firstIndex = checked((uint)_indices.Count);
        _indices.Add(vertexBase);
        _indices.Add(checked(vertexBase + 1));
        _indices.Add(checked(vertexBase + 2));
        _indices.Add(checked(vertexBase + 2));
        _indices.Add(checked(vertexBase + 3));
        _indices.Add(vertexBase);

        if (_batches.Count > 0 && _batches[^1].Scissor == scissor)
        {
            var previous = _batches[^1];
            _batches[^1] = previous with { IndexCount = checked(previous.IndexCount + 6) };
        }
        else
            _batches.Add(new UiBatch(scissor, firstIndex, 6));
    }

    private static bool TryGetPhysicalBounds(
        Rect bounds,
        uint framebufferWidth,
        uint framebufferHeight,
        double uiScale,
        out PhysicalBounds physicalBounds)
    {
        var left = ScaleAndClamp(bounds.X, uiScale, framebufferWidth);
        var top = ScaleAndClamp(bounds.Y, uiScale, framebufferHeight);
        var right = ScaleEndAndClamp(bounds.X, bounds.Width, uiScale, framebufferWidth);
        var bottom = ScaleEndAndClamp(bounds.Y, bounds.Height, uiScale, framebufferHeight);
        physicalBounds = new PhysicalBounds(left, top, right, bottom);
        return right > left && bottom > top;
    }

    private static bool TryGetScissor(
        Rect? clip,
        uint framebufferWidth,
        uint framebufferHeight,
        double uiScale,
        out UiScissor scissor)
    {
        if (clip is null)
        {
            scissor = new UiScissor(0, 0, framebufferWidth, framebufferHeight);
            return true;
        }

        var value = clip.Value;
        var left = Math.Floor(ScaleAndClamp(value.X, uiScale, framebufferWidth));
        var top = Math.Floor(ScaleAndClamp(value.Y, uiScale, framebufferHeight));
        var right = Math.Ceiling(ScaleEndAndClamp(value.X, value.Width, uiScale, framebufferWidth));
        var bottom = Math.Ceiling(ScaleEndAndClamp(value.Y, value.Height, uiScale, framebufferHeight));
        if (right <= left || bottom <= top)
        {
            scissor = default;
            return false;
        }

        scissor = new UiScissor(
            checked((int)left),
            checked((int)top),
            checked((uint)(right - left)),
            checked((uint)(bottom - top)));
        return true;
    }

    private static bool Intersects(PhysicalBounds bounds, UiScissor scissor)
    {
        var scissorRight = (double)scissor.X + scissor.Width;
        var scissorBottom = (double)scissor.Y + scissor.Height;
        return bounds.Right > scissor.X &&
               bounds.Bottom > scissor.Y &&
               bounds.Left < scissorRight &&
               bounds.Top < scissorBottom;
    }

    private static double ScaleAndClamp(double value, double scale, uint maximum) =>
        Math.Clamp(value * scale, 0, maximum);

    private static double ScaleEndAndClamp(double origin, double length, double scale, uint maximum) =>
        Math.Clamp((origin + length) * scale, 0, maximum);

    private static float ToColorChannel(byte value, bool convertSrgbToLinear)
    {
        var channel = value / 255f;
        if (!convertSrgbToLinear)
            return channel;
        return channel <= 0.04045f
            ? channel / 12.92f
            : MathF.Pow((channel + 0.055f) / 1.055f, 2.4f);
    }

    private readonly record struct PhysicalBounds(
        double Left,
        double Top,
        double Right,
        double Bottom);
}

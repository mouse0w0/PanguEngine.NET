using System.Runtime.InteropServices;
using PanguEngine.Graphics;

namespace PanguEngine.Client.UI.Rendering;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct UiVertex
{
    internal const uint SizeInBytes = 48;

    internal static readonly VertexInputDescription SolidVertexInput = new(
        [new VertexBufferLayoutDescription(0, SizeInBytes)],
        [
            new VertexAttributeDescription(0, 0, VertexAttributeFormat.Float32x2, 0),
            new VertexAttributeDescription(1, 0, VertexAttributeFormat.Float32x4, 8)
        ]);

    internal static readonly VertexInputDescription ImageVertexInput = new(
        [new VertexBufferLayoutDescription(0, SizeInBytes)],
        [
            new VertexAttributeDescription(0, 0, VertexAttributeFormat.Float32x2, 0),
            new VertexAttributeDescription(1, 0, VertexAttributeFormat.Float32x4, 8),
            new VertexAttributeDescription(2, 0, VertexAttributeFormat.Float32x2, 24),
            new VertexAttributeDescription(3, 0, VertexAttributeFormat.Float32x4, 32)
        ]);

    internal UiVertex(
        float x,
        float y,
        float r,
        float g,
        float b,
        float a,
        float u = 0,
        float v = 0,
        float clampMinU = 0,
        float clampMinV = 0,
        float clampMaxU = 0,
        float clampMaxV = 0)
    {
        X = x;
        Y = y;
        R = r;
        G = g;
        B = b;
        A = a;
        U = u;
        V = v;
        ClampMinU = clampMinU;
        ClampMinV = clampMinV;
        ClampMaxU = clampMaxU;
        ClampMaxV = clampMaxV;
    }

    internal readonly float X;
    internal readonly float Y;
    internal readonly float R;
    internal readonly float G;
    internal readonly float B;
    internal readonly float A;
    internal readonly float U;
    internal readonly float V;
    internal readonly float ClampMinU;
    internal readonly float ClampMinV;
    internal readonly float ClampMaxU;
    internal readonly float ClampMaxV;
}

internal readonly record struct UiScissor(int X, int Y, uint Width, uint Height);

internal enum UiMaterialKind
{
    Solid,
    Image
}

internal readonly record struct UiBatchMaterial(
    UiMaterialKind Kind,
    ulong ResourceId,
    DescriptorSet? DescriptorSet,
    ImageSamplingMode SamplingMode)
{
    internal static UiBatchMaterial Solid =>
        new(UiMaterialKind.Solid, 0, null, ImageSamplingMode.Linear);
}

internal readonly record struct UiImageRenderBinding(
    ulong ResourceId,
    DescriptorSet DescriptorSet);

internal readonly record struct UiBatch
{
    internal UiBatch(
        UiScissor scissor,
        uint firstIndex,
        uint indexCount)
        : this(scissor, UiBatchMaterial.Solid, firstIndex, indexCount)
    {
    }

    internal UiBatch(
        UiScissor scissor,
        UiBatchMaterial material,
        uint firstIndex,
        uint indexCount)
    {
        Scissor = scissor;
        Material = material;
        FirstIndex = firstIndex;
        IndexCount = indexCount;
    }

    internal UiScissor Scissor { get; }
    internal UiBatchMaterial Material { get; }
    internal uint FirstIndex { get; }
    internal uint IndexCount { get; }
}

internal delegate UiImageRenderBinding? UiImageResolver(UiDrawImageCommand command);

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
        bool convertSrgbToLinear,
        UiImageResolver? imageResolver = null)
    {
        ArgumentNullException.ThrowIfNull(commands);
        var uiScale = commands.Scale;
        if (!double.IsFinite(uiScale) || uiScale <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(commands.Scale),
                "UI drawing command scale must be finite and greater than zero.");
        }

        _vertices.Clear();
        _indices.Clear();
        _batches.Clear();
        if (framebufferWidth == 0 || framebufferHeight == 0)
            return;

        foreach (var command in commands)
        {
            switch (command)
            {
                case UiFillRectangleCommand rectangle:
                    AppendRectangle(
                        rectangle,
                        framebufferWidth,
                        framebufferHeight,
                        uiScale,
                        convertSrgbToLinear);
                    break;
                case UiDrawImageCommand image:
                    if (imageResolver is null)
                        throw new NotSupportedException("Image drawing requires an image resource resolver.");
                    if (imageResolver(image) is { } binding)
                    {
                        AppendImage(
                            image,
                            binding,
                            framebufferWidth,
                            framebufferHeight,
                            uiScale);
                    }
                    break;
                default:
                    throw new NotSupportedException($"UI draw command '{command.GetType().Name}' is not supported.");
            }
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
        AppendGeometry(
            bounds,
            scissor,
            UiBatchMaterial.Solid,
            r,
            g,
            b,
            a,
            0,
            0,
            0,
            0,
            0,
            0);
    }

    private void AppendImage(
        UiDrawImageCommand command,
        UiImageRenderBinding binding,
        uint framebufferWidth,
        uint framebufferHeight,
        double uiScale)
    {
        if (!TryGetPhysicalBounds(command.Bounds, framebufferWidth, framebufferHeight, uiScale, out var bounds) ||
            !TryGetScissor(command.Clip, framebufferWidth, framebufferHeight, uiScale, out var scissor) ||
            !Intersects(bounds, scissor))
        {
            return;
        }

        var source = command.SourceRect;
        var imageWidth = (double)command.Image.PixelWidth;
        var imageHeight = (double)command.Image.PixelHeight;
        var u0 = source.X / imageWidth;
        var v0 = source.Y / imageHeight;
        var u1 = (source.X + source.Width) / imageWidth;
        var v1 = (source.Y + source.Height) / imageHeight;
        var clampMinU = (source.X + 0.5) / imageWidth;
        var clampMinV = (source.Y + 0.5) / imageHeight;
        var clampMaxU = (source.X + source.Width - 0.5) / imageWidth;
        var clampMaxV = (source.Y + source.Height - 0.5) / imageHeight;
        var material = new UiBatchMaterial(
            UiMaterialKind.Image,
            binding.ResourceId,
            binding.DescriptorSet,
            command.SamplingMode);
        AppendGeometry(
            bounds,
            scissor,
            material,
            1,
            1,
            1,
            (float)command.Opacity,
            (float)u0,
            (float)v0,
            (float)clampMinU,
            (float)clampMinV,
            (float)clampMaxU,
            (float)clampMaxV,
            (float)u1,
            (float)v1);
    }

    private void AppendGeometry(
        PhysicalBounds bounds,
        UiScissor scissor,
        UiBatchMaterial material,
        float r,
        float g,
        float b,
        float a,
        float u0,
        float v0,
        float clampMinU,
        float clampMinV,
        float clampMaxU,
        float clampMaxV,
        float u1 = 0,
        float v1 = 0)
    {
        var vertexBase = checked((uint)_vertices.Count);
        _vertices.Add(new UiVertex(
            (float)bounds.Left,
            (float)bounds.Top,
            r,
            g,
            b,
            a,
            u0,
            v0,
            clampMinU,
            clampMinV,
            clampMaxU,
            clampMaxV));
        _vertices.Add(new UiVertex(
            (float)bounds.Right,
            (float)bounds.Top,
            r,
            g,
            b,
            a,
            u1,
            v0,
            clampMinU,
            clampMinV,
            clampMaxU,
            clampMaxV));
        _vertices.Add(new UiVertex(
            (float)bounds.Right,
            (float)bounds.Bottom,
            r,
            g,
            b,
            a,
            u1,
            v1,
            clampMinU,
            clampMinV,
            clampMaxU,
            clampMaxV));
        _vertices.Add(new UiVertex(
            (float)bounds.Left,
            (float)bounds.Bottom,
            r,
            g,
            b,
            a,
            u0,
            v1,
            clampMinU,
            clampMinV,
            clampMaxU,
            clampMaxV));

        var firstIndex = checked((uint)_indices.Count);
        _indices.Add(vertexBase);
        _indices.Add(checked(vertexBase + 1));
        _indices.Add(checked(vertexBase + 2));
        _indices.Add(checked(vertexBase + 2));
        _indices.Add(checked(vertexBase + 3));
        _indices.Add(vertexBase);

        if (_batches.Count > 0 &&
            _batches[^1].Scissor == scissor &&
            SameMaterial(_batches[^1].Material, material))
        {
            var previous = _batches[^1];
            _batches[^1] = new UiBatch(
                previous.Scissor,
                previous.Material,
                previous.FirstIndex,
                checked(previous.IndexCount + 6));
        }
        else
        {
            _batches.Add(new UiBatch(scissor, material, firstIndex, 6));
        }
    }

    private static bool SameMaterial(UiBatchMaterial first, UiBatchMaterial second) =>
        first.Kind == second.Kind &&
        first.ResourceId == second.ResourceId &&
        first.SamplingMode == second.SamplingMode;

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

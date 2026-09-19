using System.Runtime.InteropServices;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Graphics;
using PanguEngine.Graphics.Text;

namespace PanguEngine.Client.UI.Rendering;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct UiVertex
{
    private const int TextureIndexShift = 8;

    internal const uint SizeInBytes = 52;

    internal static readonly VertexInputDescription VertexInput = new(
        [new VertexBufferLayoutDescription(0, SizeInBytes)],
        [
            new VertexAttributeDescription(0, 0, VertexAttributeFormat.Float32x2, 0),
            new VertexAttributeDescription(1, 0, VertexAttributeFormat.Float32x4, 8),
            new VertexAttributeDescription(2, 0, VertexAttributeFormat.Float32x2, 24),
            new VertexAttributeDescription(3, 0, VertexAttributeFormat.Float32x4, 32),
            new VertexAttributeDescription(4, 0, VertexAttributeFormat.UInt32, 48)
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
        float clampMaxV = 0,
        UiMaterialKind materialKind = UiMaterialKind.Solid,
        uint textureIndex = 0)
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
        MaterialData = (textureIndex << TextureIndexShift) | (uint)materialKind;
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
    internal readonly uint MaterialData;
}

internal readonly record struct UiScissor(int X, int Y, uint Width, uint Height);

internal enum UiMaterialKind : uint
{
    Solid,
    ImageNearest,
    ImageLinear,
    TextMask
}

internal readonly record struct UiImageRenderBinding(
    uint TextureIndex,
    uint TextureWidth,
    uint TextureHeight,
    UiImageAtlasRegion Region);

internal readonly record struct UiGlyphRenderBinding(
    uint TextureIndex,
    uint PageWidth,
    uint PageHeight,
    GlyphAtlasRegion Region,
    int Left,
    int Top);

internal readonly record struct UiBatch
{
    internal UiBatch(
        UiScissor scissor,
        uint firstIndex,
        uint indexCount)
    {
        Scissor = scissor;
        FirstIndex = firstIndex;
        IndexCount = indexCount;
    }

    internal UiScissor Scissor { get; }
    internal uint FirstIndex { get; }
    internal uint IndexCount { get; }
}

internal delegate UiImageRenderBinding? UiImageResolver(UiDrawImageCommand command);

internal delegate UiGlyphRenderBinding? UiGlyphResolver(GlyphRasterKey key);

internal sealed class UiDrawBuilder
{
    private readonly List<UiVertex> _vertices = [];
    private readonly List<uint> _indices = [];
    private readonly List<UiBatch> _batches = [];
    private readonly List<DrawingState> _states = [];

    internal ReadOnlySpan<UiVertex> Vertices => CollectionsMarshal.AsSpan(_vertices);
    internal ReadOnlySpan<uint> Indices => CollectionsMarshal.AsSpan(_indices);
    internal ReadOnlySpan<UiBatch> Batches => CollectionsMarshal.AsSpan(_batches);
    internal int RectangleCount => _vertices.Count / 4;

    internal void Build(
        UiDrawCommandList commands,
        uint framebufferWidth,
        uint framebufferHeight,
        bool convertSrgbToLinear,
        UiImageResolver? imageResolver = null,
        UiGlyphResolver? glyphResolver = null)
    {
        ArgumentNullException.ThrowIfNull(commands);
        _vertices.Clear();
        _indices.Clear();
        _batches.Clear();
        if (framebufferWidth == 0 || framebufferHeight == 0)
            return;

        var state = new DrawingState(0, 0, 1, null, 1);
        try
        {
            foreach (var command in commands)
            {
                switch (command)
                {
                    case UiPushTransformCommand transform:
                        _states.Add(state);
                        var scale = Finite(state.Scale * transform.Scale);
                        if (scale <= 0)
                            throw new InvalidOperationException("UI drawing produced a non-positive scale.");
                        state = state with
                        {
                            X = Finite(state.X + state.Scale * transform.Translation.X),
                            Y = Finite(state.Y + state.Scale * transform.Translation.Y),
                            Scale = scale
                        };
                        continue;
                    case UiPushClipCommand clip:
                        _states.Add(state);
                        var bounds = Transform(clip.Clip, state);
                        if (state.Clip is { } previousClip)
                        {
                            var left = Math.Max(previousClip.X, bounds.X);
                            var top = Math.Max(previousClip.Y, bounds.Y);
                            var right = Math.Min(previousClip.X + previousClip.Width, bounds.X + bounds.Width);
                            var bottom = Math.Min(previousClip.Y + previousClip.Height, bounds.Y + bounds.Height);
                            bounds = right <= left || bottom <= top
                                ? Rect.Zero
                                : new Rect(left, top, right - left, bottom - top);
                        }

                        state = state with { Clip = bounds };
                        continue;
                    case UiPushOpacityCommand opacity:
                        _states.Add(state);
                        state = state with { Opacity = state.Opacity * opacity.Opacity };
                        continue;
                    case UiPopCommand:
                        state = _states[^1];
                        _states.RemoveAt(_states.Count - 1);
                        continue;
                }

                if (state.Opacity == 0 ||
                    !TryGetScissor(state.Clip, framebufferWidth, framebufferHeight, out var scissor))
                {
                    continue;
                }

                switch (command)
                {
                    case UiFillRectangleCommand rectangle:
                        AppendRectangle(
                            rectangle,
                            framebufferWidth,
                            framebufferHeight,
                            state,
                            scissor,
                            convertSrgbToLinear);
                        break;
                    case UiDrawImageCommand image:
                        if (!TryGetPhysicalBounds(image.Bounds, framebufferWidth, framebufferHeight, state, out var imageBounds) ||
                            !Intersects(imageBounds, scissor))
                        {
                            break;
                        }
                        if (imageResolver is null)
                            throw new NotSupportedException("Image drawing requires an image resource resolver.");
                        if (imageResolver(image) is { } binding)
                        {
                            AppendImage(
                                image,
                                binding,
                                imageBounds,
                                scissor,
                                state.Opacity);
                        }

                        break;
                    case UiDrawTextCommand text:
                        if (glyphResolver is null)
                            throw new NotSupportedException("Text drawing requires a glyph resource resolver.");
                        AppendText(text, glyphResolver, state, scissor, convertSrgbToLinear);
                        break;
                    default:
                        throw new NotSupportedException($"UI draw command '{command.GetType().Name}' is not supported.");
                }
            }
        }
        catch
        {
            _vertices.Clear();
            _indices.Clear();
            _batches.Clear();
            throw;
        }
        finally
        {
            _states.Clear();
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
        DrawingState state,
        UiScissor scissor,
        bool convertSrgbToLinear)
    {
        if (!TryGetPhysicalBounds(command.Bounds, framebufferWidth, framebufferHeight, state, out var bounds) ||
            !Intersects(bounds, scissor))
        {
            return;
        }

        var color = command.Color;
        var r = ToColorChannel(color.R, convertSrgbToLinear);
        var g = ToColorChannel(color.G, convertSrgbToLinear);
        var b = ToColorChannel(color.B, convertSrgbToLinear);
        var a = (float)(color.A / 255.0 * state.Opacity);
        AppendGeometry(
            bounds,
            scissor,
            UiMaterialKind.Solid,
            0,
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
        PhysicalBounds bounds,
        UiScissor scissor,
        double opacity)
    {
        var source = command.SourceRect;
        var textureWidth = (double)binding.TextureWidth;
        var textureHeight = (double)binding.TextureHeight;
        var sourceX = binding.Region.X + source.X;
        var sourceY = binding.Region.Y + source.Y;
        var u0 = sourceX / textureWidth;
        var v0 = sourceY / textureHeight;
        var u1 = (sourceX + source.Width) / textureWidth;
        var v1 = (sourceY + source.Height) / textureHeight;
        var (clampMinU, clampMaxU) = GetNormalizedClamp(sourceX, source.Width, textureWidth);
        var (clampMinV, clampMaxV) = GetNormalizedClamp(sourceY, source.Height, textureHeight);
        var materialKind = command.SamplingMode == ImageSamplingMode.Nearest
            ? UiMaterialKind.ImageNearest
            : UiMaterialKind.ImageLinear;
        AppendGeometry(
            bounds,
            scissor,
            materialKind,
            binding.TextureIndex,
            1,
            1,
            1,
            (float)opacity,
            (float)u0,
            (float)v0,
            clampMinU,
            clampMinV,
            clampMaxU,
            clampMaxV,
            (float)u1,
            (float)v1);
    }

    private void AppendText(
        UiDrawTextCommand command,
        UiGlyphResolver glyphResolver,
        DrawingState state,
        UiScissor scissor,
        bool convertSrgbToLinear)
    {
        var pixelSize = GlyphRasterization.GetPixelSize(command.FontSize, state.Scale);
        var color = command.Color;
        var r = ToColorChannel(color.R, convertSrgbToLinear);
        var g = ToColorChannel(color.G, convertSrgbToLinear);
        var b = ToColorChannel(color.B, convertSrgbToLinear);
        var a = (float)(color.A / 255.0 * state.Opacity);
        foreach (var line in command.Layout.Lines)
        {
            foreach (var run in line.GlyphRuns)
            {
                foreach (var glyph in run.Glyphs)
                {
                    var key = new GlyphRasterKey(
                        run.FontFace,
                        pixelSize,
                        glyph.GlyphId,
                        GlyphRasterizationMode.Grayscale);
                    if (glyphResolver(key) is not { } binding)
                        continue;

                    var penX = Finite(state.X + (command.Origin.X + glyph.X + glyph.XOffset) * state.Scale);
                    var baselineY = Finite(state.Y + (command.Origin.Y + glyph.Y + glyph.YOffset) * state.Scale);
                    var bounds = new PhysicalBounds(
                        Finite(penX + binding.Left),
                        Finite(baselineY - binding.Top),
                        Finite(penX + binding.Left + binding.Region.Width),
                        Finite(baselineY - binding.Top + binding.Region.Height));
                    if (!Intersects(bounds, scissor))
                        continue;

                    var pageWidth = (double)binding.PageWidth;
                    var pageHeight = (double)binding.PageHeight;
                    var region = binding.Region;
                    var u0 = region.X / pageWidth;
                    var v0 = region.Y / pageHeight;
                    var u1 = (region.X + region.Width) / pageWidth;
                    var v1 = (region.Y + region.Height) / pageHeight;
                    var (clampMinU, clampMaxU) = GetNormalizedClamp(
                        region.X,
                        region.Width,
                        pageWidth);
                    var (clampMinV, clampMaxV) = GetNormalizedClamp(
                        region.Y,
                        region.Height,
                        pageHeight);
                    AppendGeometry(
                        bounds,
                        scissor,
                        UiMaterialKind.TextMask,
                        binding.TextureIndex,
                        r,
                        g,
                        b,
                        a,
                        (float)u0,
                        (float)v0,
                        clampMinU,
                        clampMinV,
                        clampMaxU,
                        clampMaxV,
                        (float)u1,
                        (float)v1);
                }
            }
        }
    }

    private void AppendGeometry(
        PhysicalBounds bounds,
        UiScissor scissor,
        UiMaterialKind materialKind,
        uint textureIndex,
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
            clampMaxV,
            materialKind,
            textureIndex));
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
            clampMaxV,
            materialKind,
            textureIndex));
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
            clampMaxV,
            materialKind,
            textureIndex));
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
            clampMaxV,
            materialKind,
            textureIndex));

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
            _batches[^1] = new UiBatch(
                previous.Scissor,
                previous.FirstIndex,
                checked(previous.IndexCount + 6));
        }
        else
        {
            _batches.Add(new UiBatch(scissor, firstIndex, 6));
        }
    }

    private static (float Min, float Max) GetNormalizedClamp(
        double origin,
        double length,
        double textureLength)
    {
        var min = length >= 1 ? origin + 0.5 : origin + length / 2;
        var max = length >= 1 ? origin + length - 0.5 : min;
        return ((float)(min / textureLength), (float)(max / textureLength));
    }

    private static bool TryGetPhysicalBounds(
        Rect bounds,
        uint framebufferWidth,
        uint framebufferHeight,
        DrawingState state,
        out PhysicalBounds physicalBounds)
    {
        var transformed = Transform(bounds, state);
        if (state.Clip is { } clip &&
            (transformed.X + transformed.Width <= clip.X ||
             transformed.Y + transformed.Height <= clip.Y ||
             transformed.X >= clip.X + clip.Width ||
             transformed.Y >= clip.Y + clip.Height))
        {
            physicalBounds = default;
            return false;
        }

        var left = Math.Clamp(transformed.X, 0, framebufferWidth);
        var top = Math.Clamp(transformed.Y, 0, framebufferHeight);
        var right = Math.Clamp(transformed.X + transformed.Width, 0, framebufferWidth);
        var bottom = Math.Clamp(transformed.Y + transformed.Height, 0, framebufferHeight);
        physicalBounds = new PhysicalBounds(left, top, right, bottom);
        return right > left && bottom > top;
    }

    private static bool TryGetScissor(
        Rect? clip,
        uint framebufferWidth,
        uint framebufferHeight,
        out UiScissor scissor)
    {
        if (clip is null)
        {
            scissor = new UiScissor(0, 0, framebufferWidth, framebufferHeight);
            return true;
        }

        var value = clip.Value;
        if (value.Width == 0 || value.Height == 0)
        {
            scissor = default;
            return false;
        }

        var left = Math.Floor(Math.Clamp(value.X, 0, framebufferWidth));
        var top = Math.Floor(Math.Clamp(value.Y, 0, framebufferHeight));
        var right = Math.Ceiling(Math.Clamp(value.X + value.Width, 0, framebufferWidth));
        var bottom = Math.Ceiling(Math.Clamp(value.Y + value.Height, 0, framebufferHeight));
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

    private static Rect Transform(Rect bounds, DrawingState state)
    {
        var x = Finite(state.X + bounds.X * state.Scale);
        var y = Finite(state.Y + bounds.Y * state.Scale);
        var width = Finite(bounds.Width * state.Scale);
        var height = Finite(bounds.Height * state.Scale);
        Finite(x + width);
        Finite(y + height);
        return new Rect(x, y, width, height);
    }

    private static double Finite(double value)
    {
        if (!double.IsFinite(value))
            throw new InvalidOperationException("UI drawing produced a non-finite coordinate or scale.");
        return value;
    }

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

    private readonly record struct DrawingState(
        double X,
        double Y,
        double Scale,
        Rect? Clip,
        double Opacity);
}

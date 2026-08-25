using PanguEngine.Graphics.Text;

namespace PanguEngine.Client.UI;

internal readonly record struct UiDrawingState(
    double OriginX,
    double OriginY,
    Rect? Clip,
    bool IsClipEmpty,
    double Opacity);

/// <summary>
/// Provides constrained drawing operations for a single UI node.
/// </summary>
public sealed class UiDrawingContext
{
    private readonly List<UiDrawCommand> _commands;
    private readonly List<StateEntry> _states = [];
    private UiDrawingState _state;
    private int _nextToken;
    private bool _isActive = true;

    internal UiDrawingContext(
        List<UiDrawCommand> commands,
        UiDrawingState state)
    {
        _commands = commands;
        _state = state;
    }

    /// <summary>
    /// Appends a visible solid-color rectangle using local node coordinates.
    /// </summary>
    /// <param name="bounds">The rectangle in local node coordinates.</param>
    /// <param name="color">The non-premultiplied fill color.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when this context is no longer active or coordinate calculations are not finite.
    /// </exception>
    public void FillRectangle(Rect bounds, Color color)
    {
        VerifyActive();
        if (bounds.Width == 0 ||
            bounds.Height == 0 ||
            color.A == 0 ||
            _state.Opacity == 0 ||
            _state.IsClipEmpty)
        {
            return;
        }

        var screenBounds = Translate(bounds, _state.OriginX, _state.OriginY);
        if (_state.Clip is { } clip && !TryIntersect(screenBounds, clip, out _))
            return;

        _commands.Add(
            new UiFillRectangleCommand(
                screenBounds,
                color,
                _state.Clip,
                _state.Opacity));
    }

    internal void FillRectangle(
        Rect bounds,
        Brush brush)
    {
        switch (brush)
        {
            case SolidColorBrush solidColorBrush:
                FillRectangle(bounds, solidColorBrush.Color);
                return;
            case ImageBrush imageBrush:
                FillImageRectangle(bounds, imageBrush);
                return;
            case NineSliceImageBrush nineSliceImageBrush:
                FillNineSliceRectangle(bounds, nineSliceImageBrush);
                return;
            default:
                throw new NotSupportedException(
                    $"Brush type '{brush.GetType().FullName}' is not supported for rectangle fills.");
        }
    }

    internal void FillBorder(
        Rect outerBounds,
        Rect innerBounds,
        Brush brush)
    {
        switch (brush)
        {
            case SolidColorBrush solidColorBrush:
                FillSolidBorder(outerBounds, innerBounds, solidColorBrush.Color);
                return;
            case ImageBrush imageBrush:
                FillImageBorder(outerBounds, innerBounds, imageBrush);
                return;
            case NineSliceImageBrush nineSliceImageBrush:
                FillNineSliceBorder(outerBounds, innerBounds, nineSliceImageBrush);
                return;
            default:
                throw new NotSupportedException(
                    $"Brush type '{brush.GetType().FullName}' is not supported for border fills.");
        }
    }

    /// <summary>
    /// Appends an image using the requested destination, source region, and sampling mode.
    /// </summary>
    /// <param name="bounds">The destination rectangle in local node coordinates.</param>
    /// <param name="image">The image source.</param>
    /// <param name="sourceRect">The source region in image pixel coordinates, or the full image when null.</param>
    /// <param name="samplingMode">The image sampling mode.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the source region is outside the image.</exception>
    /// <exception cref="InvalidOperationException">Thrown when this context is no longer active.</exception>
    public void DrawImage(
        Rect bounds,
        UiImage image,
        Rect? sourceRect = null,
        ImageSamplingMode samplingMode = ImageSamplingMode.Linear)
    {
        VerifyActive();
        ArgumentNullException.ThrowIfNull(image);

        var resolvedSourceRect = sourceRect ?? image.FullSourceRect;
        if (!image.ContainsSourceRect(resolvedSourceRect))
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceRect),
                "The image source region must be contained within the image.");
        }

        if (bounds.Width == 0 ||
            bounds.Height == 0 ||
            _state.Opacity == 0 ||
            _state.IsClipEmpty ||
            resolvedSourceRect.Width == 0 ||
            resolvedSourceRect.Height == 0)
        {
            return;
        }

        var screenBounds = Translate(bounds, _state.OriginX, _state.OriginY);
        if (_state.Clip is { } clip && !TryIntersect(screenBounds, clip, out _))
            return;

        _commands.Add(
            new UiDrawImageCommand(
                screenBounds,
                image,
                resolvedSourceRect,
                samplingMode,
                _state.Clip,
                _state.Opacity));
    }

    /// <summary>
    /// Appends an immutable text layout using local node coordinates.
    /// </summary>
    /// <param name="origin">The layout origin in local node coordinates.</param>
    /// <param name="layout">The immutable CPU text layout.</param>
    /// <param name="fontSize">The font size in logical pixels used to rasterize the layout.</param>
    /// <param name="color">The non-premultiplied text color.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layout"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when this context is no longer active.</exception>
    public void DrawText(
        Point origin,
        TextLayout layout,
        double fontSize,
        Color color)
    {
        VerifyActive();
        ArgumentNullException.ThrowIfNull(layout);
        if (color.A == 0 ||
            _state.Opacity == 0 ||
            _state.IsClipEmpty ||
            !ContainsGlyph(layout))
        {
            return;
        }

        _commands.Add(new UiDrawTextCommand(
            new Point(
                AddCoordinate(_state.OriginX, origin.X),
                AddCoordinate(_state.OriginY, origin.Y)),
            layout,
            fontSize,
            color,
            _state.Clip,
            _state.Opacity));
    }

    /// <summary>
    /// Pushes a rectangular clip expressed in local node coordinates.
    /// </summary>
    /// <param name="clip">The local clip rectangle.</param>
    /// <returns>A scope that restores the previous clip when disposed.</returns>
    /// <remarks>A zero-area clip suppresses drawing until the returned scope is disposed.</remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when this context is no longer active or coordinate calculations are not finite.
    /// </exception>
    public UiDrawingScope PushClip(Rect clip)
    {
        VerifyActive();
        var screenClip = Translate(clip, _state.OriginX, _state.OriginY);
        return PushState(ApplyClip(_state, screenClip));
    }

    /// <summary>
    /// Pushes a multiplicative opacity factor.
    /// </summary>
    /// <param name="opacity">The finite opacity factor from zero through one.</param>
    /// <returns>A scope that restores the previous opacity when disposed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="opacity"/> is not finite or is outside the range from zero through one.
    /// </exception>
    /// <exception cref="InvalidOperationException">Thrown when this context is no longer active.</exception>
    public UiDrawingScope PushOpacity(double opacity)
    {
        VerifyActive();
        if (!double.IsFinite(opacity) || opacity < 0 || opacity > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(opacity),
                "Opacity must be finite and between zero and one.");
        }

        return PushState(_state with { Opacity = _state.Opacity * opacity });
    }

    internal static double AddCoordinate(double left, double right)
    {
        var result = left + right;
        if (!double.IsFinite(result))
            throw new InvalidOperationException("UI drawing produced a non-finite coordinate.");

        return result;
    }

    internal static Rect Translate(Rect rect, double x, double y) =>
        new(
            AddCoordinate(x, rect.X),
            AddCoordinate(y, rect.Y),
            rect.Width,
            rect.Height);

    internal static UiDrawingState ApplyClip(
        UiDrawingState state,
        Rect screenClip)
    {
        if (state.IsClipEmpty || screenClip.Width == 0 || screenClip.Height == 0)
            return state with { Clip = null, IsClipEmpty = true };
        if (state.Clip is null)
            return state with { Clip = screenClip };
        if (TryIntersect(state.Clip.Value, screenClip, out var intersection))
            return state with { Clip = intersection };

        return state with { Clip = null, IsClipEmpty = true };
    }

    internal static bool TryIntersect(Rect first, Rect second, out Rect intersection)
    {
        var left = Math.Max(first.X, second.X);
        var top = Math.Max(first.Y, second.Y);
        var right = Math.Min(
            AddCoordinate(first.X, first.Width),
            AddCoordinate(second.X, second.Width));
        var bottom = Math.Min(
            AddCoordinate(first.Y, first.Height),
            AddCoordinate(second.Y, second.Height));
        if (right <= left || bottom <= top)
        {
            intersection = Rect.Zero;
            return false;
        }

        intersection = new Rect(left, top, right - left, bottom - top);
        return true;
    }

    internal void Complete()
    {
        VerifyActive();
        _isActive = false;
        if (_states.Count == 0)
            return;

        _states.Clear();
        throw new InvalidOperationException("Every UI drawing scope must be disposed before drawing returns.");
    }

    internal void Abort()
    {
        _states.Clear();
        _isActive = false;
    }

    internal void Pop(int token)
    {
        VerifyActive();
        var lastIndex = _states.Count - 1;
        if (lastIndex < 0 || _states[lastIndex].Token != token)
            throw new InvalidOperationException("UI drawing scopes must be disposed once in last-in-first-out order.");

        _state = _states[lastIndex].State;
        _states.RemoveAt(lastIndex);
    }

    private void FillImageRectangle(
        Rect bounds,
        ImageBrush brush)
    {
        var sourceRect = brush.SourceRect;
        if (sourceRect.Width == 0 || sourceRect.Height == 0)
        {
            DrawImage(bounds, brush.Source, sourceRect, brush.SamplingMode);
            return;
        }

        var destinationBounds = UiImageLayout.GetDestinationBounds(
            bounds,
            sourceRect.Width,
            sourceRect.Height,
            brush.Stretch);
        using (PushClip(bounds))
        {
            DrawImage(
                destinationBounds,
                brush.Source,
                sourceRect,
                brush.SamplingMode);
        }
    }

    private void FillSolidBorder(Rect outerBounds, Rect innerBounds, Color color)
    {
        Span<Rect> bounds = stackalloc Rect[4];
        GetBorderBounds(outerBounds, innerBounds, bounds);
        foreach (var edgeBounds in bounds)
            FillRectangle(edgeBounds, color);
    }

    private void FillImageBorder(
        Rect outerBounds,
        Rect innerBounds,
        ImageBrush brush)
    {
        var sourceRect = brush.SourceRect;
        if (sourceRect.Width == 0 || sourceRect.Height == 0)
        {
            DrawImage(outerBounds, brush.Source, sourceRect, brush.SamplingMode);
            return;
        }

        var destinationBounds = UiImageLayout.GetDestinationBounds(
            outerBounds,
            sourceRect.Width,
            sourceRect.Height,
            brush.Stretch);
        Span<Rect> edgeBounds = stackalloc Rect[4];
        GetBorderBounds(outerBounds, innerBounds, edgeBounds);
        foreach (var edge in edgeBounds)
        {
            using (PushClip(edge))
            {
                DrawImage(
                    destinationBounds,
                    brush.Source,
                    sourceRect,
                    brush.SamplingMode);
            }
        }
    }

    private void FillNineSliceRectangle(Rect bounds, NineSliceImageBrush brush)
    {
        Span<double> destinationX = stackalloc double[4];
        Span<double> destinationY = stackalloc double[4];
        GetBackgroundDestinationCuts(
            bounds,
            brush.Slice,
            destinationX,
            destinationY);
        AppendNineSlice(
            brush.Source,
            brush.SourceRect,
            brush.Slice,
            destinationX,
            destinationY,
            brush.SamplingMode,
            drawCenter: true);
    }

    private void FillNineSliceBorder(
        Rect outerBounds,
        Rect innerBounds,
        NineSliceImageBrush brush)
    {
        Span<double> destinationX = stackalloc double[4];
        Span<double> destinationY = stackalloc double[4];
        destinationX[0] = outerBounds.X;
        destinationX[1] = innerBounds.X;
        destinationX[2] = innerBounds.X + innerBounds.Width;
        destinationX[3] = outerBounds.X + outerBounds.Width;
        destinationY[0] = outerBounds.Y;
        destinationY[1] = innerBounds.Y;
        destinationY[2] = innerBounds.Y + innerBounds.Height;
        destinationY[3] = outerBounds.Y + outerBounds.Height;
        AppendNineSlice(
            brush.Source,
            brush.SourceRect,
            brush.Slice,
            destinationX,
            destinationY,
            brush.SamplingMode,
            drawCenter: false);
    }

    private void AppendNineSlice(
        UiImage image,
        Rect sourceRect,
        ImageSlice slice,
        ReadOnlySpan<double> destinationX,
        ReadOnlySpan<double> destinationY,
        ImageSamplingMode samplingMode,
        bool drawCenter)
    {
        Span<double> sourceX = stackalloc double[4];
        Span<double> sourceY = stackalloc double[4];
        GetSourceCuts(sourceRect, slice, sourceX, sourceY);

        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                if (!drawCenter && row == 1 && column == 1)
                    continue;

                DrawImage(
                    new Rect(
                        destinationX[column],
                        destinationY[row],
                        destinationX[column + 1] - destinationX[column],
                        destinationY[row + 1] - destinationY[row]),
                    image,
                    new Rect(
                        sourceX[column],
                        sourceY[row],
                        sourceX[column + 1] - sourceX[column],
                        sourceY[row + 1] - sourceY[row]),
                    samplingMode);
            }
        }
    }

    private static void GetBorderBounds(
        Rect outerBounds,
        Rect innerBounds,
        Span<Rect> bounds)
    {
        bounds[0] = new Rect(
            outerBounds.X,
            outerBounds.Y,
            outerBounds.Width,
            innerBounds.Y - outerBounds.Y);
        bounds[1] = new Rect(
            innerBounds.X + innerBounds.Width,
            innerBounds.Y,
            outerBounds.X + outerBounds.Width -
            (innerBounds.X + innerBounds.Width),
            innerBounds.Height);
        bounds[2] = new Rect(
            outerBounds.X,
            innerBounds.Y + innerBounds.Height,
            outerBounds.Width,
            outerBounds.Y + outerBounds.Height -
            (innerBounds.Y + innerBounds.Height));
        bounds[3] = new Rect(
            outerBounds.X,
            innerBounds.Y,
            innerBounds.X - outerBounds.X,
            innerBounds.Height);
    }

    private static void GetSourceCuts(
        Rect source,
        ImageSlice slice,
        Span<double> x,
        Span<double> y)
    {
        x[0] = source.X;
        x[1] = source.X + slice.Left;
        x[2] = source.X + source.Width - slice.Right;
        x[3] = source.X + source.Width;
        y[0] = source.Y;
        y[1] = source.Y + slice.Top;
        y[2] = source.Y + source.Height - slice.Bottom;
        y[3] = source.Y + source.Height;
    }

    private static void GetBackgroundDestinationCuts(
        Rect bounds,
        ImageSlice slice,
        Span<double> x,
        Span<double> y)
    {
        var fixedWidth = (double)slice.Left + slice.Right;
        var shrinkWidth = fixedWidth > bounds.Width;
        var factorX = shrinkWidth ? bounds.Width / fixedWidth : 1;
        var fixedHeight = (double)slice.Top + slice.Bottom;
        var shrinkHeight = fixedHeight > bounds.Height;
        var factorY = shrinkHeight ? bounds.Height / fixedHeight : 1;

        x[0] = bounds.X;
        x[1] = bounds.X + slice.Left * factorX;
        x[2] = shrinkWidth
            ? x[1]
            : bounds.X + bounds.Width - slice.Right;
        x[3] = bounds.X + bounds.Width;
        y[0] = bounds.Y;
        y[1] = bounds.Y + slice.Top * factorY;
        y[2] = shrinkHeight
            ? y[1]
            : bounds.Y + bounds.Height - slice.Bottom;
        y[3] = bounds.Y + bounds.Height;
    }

    private UiDrawingScope PushState(UiDrawingState state)
    {
        var token = ++_nextToken;
        _states.Add(new StateEntry(token, _state));
        _state = state;
        return new UiDrawingScope(this, token);
    }

    private static bool ContainsGlyph(TextLayout layout)
    {
        foreach (var line in layout.Lines)
        {
            foreach (var run in line.GlyphRuns)
            {
                if (run.Glyphs.Count > 0)
                    return true;
            }
        }
        return false;
    }

    private void VerifyActive()
    {
        if (!_isActive)
            throw new InvalidOperationException("The UI drawing context is no longer active.");
    }

    private readonly record struct StateEntry(int Token, UiDrawingState State);
}

/// <summary>
/// Restores a UI drawing context state when disposed.
/// </summary>
public readonly ref struct UiDrawingScope
{
    private readonly UiDrawingContext? _context;
    private readonly int _token;

    internal UiDrawingScope(UiDrawingContext context, int token)
    {
        _context = context;
        _token = token;
    }

    /// <summary>
    /// Restores the drawing state that existed before this scope was created.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the scope is disposed more than once, out of order, or after its context is inactive.
    /// </exception>
    public void Dispose() =>
        _context?.Pop(_token);
}

using PanguEngine.Graphics.Text;

namespace PanguEngine.Client.UI.Drawing;

/// <summary>
/// Provides constrained drawing operations for a single UI node.
/// </summary>
public sealed class UiDrawingContext
{
    private readonly List<UiDrawCommand> _commands;
    private readonly List<StateEntry> _states = [];
    private readonly int _ownerThreadId = Environment.CurrentManagedThreadId;
    private int _nextToken;
    private bool _isActive = true;

    internal UiDrawingContext(List<UiDrawCommand> commands)
    {
        _commands = commands;
    }

    /// <summary>
    /// Appends a solid-color rectangle using local drawing coordinates.
    /// </summary>
    /// <param name="bounds">The rectangle in the current local drawing coordinates.</param>
    /// <param name="color">The non-premultiplied fill color.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when this context is no longer active or is accessed from the wrong thread.
    /// </exception>
    public void FillRectangle(Rect bounds, Color color)
    {
        VerifyActive();
        if (bounds.Width == 0 ||
            bounds.Height == 0 ||
            color.A == 0)
        {
            return;
        }

        _commands.Add(new UiFillRectangleCommand(bounds, color));
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
    /// <param name="bounds">The destination rectangle in the current local drawing coordinates.</param>
    /// <param name="image">The image source.</param>
    /// <param name="sourceRect">The source region in image pixel coordinates, or the full image when null.</param>
    /// <param name="samplingMode">The image sampling mode.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the source region is outside the image.</exception>
    /// <exception cref="InvalidOperationException">Thrown when this context is inactive or on the wrong thread.</exception>
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
            resolvedSourceRect.Width == 0 ||
            resolvedSourceRect.Height == 0)
        {
            return;
        }

        _commands.Add(
            new UiDrawImageCommand(
                bounds,
                image,
                resolvedSourceRect,
                samplingMode));
    }

    /// <summary>
    /// Appends an immutable text layout using local drawing coordinates.
    /// </summary>
    /// <param name="origin">The layout origin in the current local drawing coordinates.</param>
    /// <param name="layout">The immutable CPU text layout.</param>
    /// <param name="fontSize">The font size in logical pixels used to rasterize the layout.</param>
    /// <param name="color">The non-premultiplied text color.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layout"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when this context is inactive or on the wrong thread.</exception>
    public void DrawText(
        Point origin,
        TextLayout layout,
        double fontSize,
        Color color)
    {
        VerifyActive();
        ArgumentNullException.ThrowIfNull(layout);
        if (color.A == 0 ||
            !ContainsGlyph(layout))
        {
            return;
        }

        _commands.Add(new UiDrawTextCommand(
            origin,
            layout,
            fontSize,
            color));
    }

    /// <summary>
    /// Pushes a translation and positive uniform scale relative to the current drawing coordinates.
    /// </summary>
    /// <param name="translation">The translation applied after the local scale.</param>
    /// <param name="scale">The finite positive uniform scale.</param>
    /// <returns>A scope that restores the previous drawing state when disposed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when scale is not finite and positive.</exception>
    /// <exception cref="InvalidOperationException">Thrown when this context is inactive or on the wrong thread.</exception>
    public UiDrawingScope PushTransform(Point translation, double scale = 1)
    {
        VerifyActive();
        if (!double.IsFinite(scale) || scale <= 0)
            throw new ArgumentOutOfRangeException(nameof(scale), "Scale must be finite and greater than zero.");

        return PushState(PushTransform(_commands, translation, scale));
    }

    /// <summary>
    /// Pushes a translation relative to the current drawing coordinates.
    /// </summary>
    /// <param name="translation">The local translation, affected by the current scale.</param>
    /// <returns>A scope that restores the previous drawing state when disposed.</returns>
    /// <exception cref="InvalidOperationException">Thrown when this context is inactive or on the wrong thread.</exception>
    public UiDrawingScope PushTranslate(Point translation) => PushTransform(translation);

    /// <summary>
    /// Pushes a positive uniform scale multiplied into the current drawing scale.
    /// </summary>
    /// <param name="scale">The finite positive relative scale.</param>
    /// <returns>A scope that restores the previous drawing state when disposed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when scale is not finite and positive.</exception>
    /// <exception cref="InvalidOperationException">Thrown when this context is inactive or on the wrong thread.</exception>
    public UiDrawingScope PushScale(double scale) => PushTransform(Point.Zero, scale);

    /// <summary>
    /// Saves the current drawing state and replaces its translation and uniform scale.
    /// </summary>
    /// <param name="translation">The absolute translation in framebuffer pixels.</param>
    /// <param name="scale">The finite positive scale from local units to framebuffer pixels.</param>
    /// <returns>A scope that restores the previous drawing state when disposed.</returns>
    /// <remarks>Established clips and opacity remain unchanged.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when scale is not finite and positive.</exception>
    /// <exception cref="InvalidOperationException">Thrown when this context is inactive or on the wrong thread.</exception>
    public UiDrawingScope SetTransform(Point translation, double scale = 1)
    {
        VerifyActive();
        if (!double.IsFinite(scale) || scale <= 0)
            throw new ArgumentOutOfRangeException(nameof(scale), "Scale must be finite and greater than zero.");

        return PushState(PushCommand(_commands, new UiSetTransformCommand(translation, scale)));
    }

    /// <summary>
    /// Saves the current drawing state and replaces only its translation.
    /// </summary>
    /// <param name="translation">The absolute translation in framebuffer pixels.</param>
    /// <returns>A scope that restores the previous drawing state when disposed.</returns>
    /// <remarks>The current scale, established clips, and opacity remain unchanged.</remarks>
    /// <exception cref="InvalidOperationException">Thrown when this context is inactive or on the wrong thread.</exception>
    public UiDrawingScope SetTranslate(Point translation)
    {
        VerifyActive();
        return PushState(PushCommand(_commands, new UiSetTranslateCommand(translation)));
    }

    /// <summary>
    /// Saves the current drawing state and replaces only its uniform scale.
    /// </summary>
    /// <param name="scale">The finite positive scale from local units to framebuffer pixels.</param>
    /// <returns>A scope that restores the previous drawing state when disposed.</returns>
    /// <remarks>The current framebuffer origin, established clips, and opacity remain unchanged.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when scale is not finite and positive.</exception>
    /// <exception cref="InvalidOperationException">Thrown when this context is inactive or on the wrong thread.</exception>
    public UiDrawingScope SetScale(double scale)
    {
        VerifyActive();
        if (!double.IsFinite(scale) || scale <= 0)
            throw new ArgumentOutOfRangeException(nameof(scale), "Scale must be finite and greater than zero.");

        return PushState(PushCommand(_commands, new UiSetScaleCommand(scale)));
    }

    /// <summary>
    /// Pushes a rectangular clip established using the current drawing transform.
    /// </summary>
    /// <param name="clip">The local clip rectangle.</param>
    /// <returns>A scope that restores the previous clip when disposed.</returns>
    /// <remarks>A zero-area clip suppresses drawing until the returned scope is disposed.</remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when this context is no longer active or is accessed from the wrong thread.
    /// </exception>
    public UiDrawingScope PushClip(Rect clip)
    {
        VerifyActive();
        return PushState(PushCommand(_commands, new UiPushClipCommand(clip)));
    }

    /// <summary>
    /// Pushes a multiplicative opacity factor.
    /// </summary>
    /// <param name="opacity">The finite opacity factor from zero through one.</param>
    /// <returns>A scope that restores the previous opacity when disposed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="opacity"/> is not finite or is outside the range from zero through one.
    /// </exception>
    /// <exception cref="InvalidOperationException">Thrown when this context is inactive or on the wrong thread.</exception>
    public UiDrawingScope PushOpacity(double opacity)
    {
        VerifyActive();
        if (!double.IsFinite(opacity) || opacity < 0 || opacity > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(opacity),
                "Opacity must be finite and between zero and one.");
        }

        return PushState(PushOpacity(_commands, opacity));
    }

    internal static int PushTransform(List<UiDrawCommand> commands, Point translation, double scale)
    {
        return translation == Point.Zero && scale == 1
            ? -1
            : PushCommand(commands, new UiPushTransformCommand(translation, scale));
    }

    internal static int PushOpacity(List<UiDrawCommand> commands, double opacity) =>
        opacity == 1 ? -1 : PushCommand(commands, new UiPushOpacityCommand(opacity));

    internal static int PushCommand(List<UiDrawCommand> commands, UiDrawCommand command)
    {
        var index = commands.Count;
        commands.Add(command);
        return index;
    }

    internal static void PopCommand(List<UiDrawCommand> commands, int pushIndex)
    {
        if (pushIndex < 0)
            return;
        if (commands.Count == pushIndex + 1)
            commands.RemoveAt(pushIndex);
        else
            commands.Add(UiPopCommand.Instance);
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

        PopCommand(_commands, _states[lastIndex].PushIndex);
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

    private UiDrawingScope PushState(int pushIndex)
    {
        var token = ++_nextToken;
        _states.Add(new StateEntry(token, pushIndex));
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
        if (_ownerThreadId != Environment.CurrentManagedThreadId)
            throw new InvalidOperationException("UI drawing requires its recording thread.");
    }

    private readonly record struct StateEntry(int Token, int PushIndex);
}

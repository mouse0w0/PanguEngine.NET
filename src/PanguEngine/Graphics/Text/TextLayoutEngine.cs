using System.Globalization;
using System.Text;
using HarfBuzzSharp;
using HbBuffer = HarfBuzzSharp.Buffer;

namespace PanguEngine.Graphics.Text;

/// <summary>
/// Creates immutable CPU text layouts.
/// </summary>
public sealed class TextLayoutEngine
{
    private readonly FontManager _fontManager;
    private readonly int _ownerThreadId = Environment.CurrentManagedThreadId;

    internal TextLayoutEngine(FontManager fontManager)
    {
        ArgumentNullException.ThrowIfNull(fontManager);
        fontManager.VerifyServiceAccess();
        _fontManager = fontManager;
    }

    internal void VerifyServiceAccess()
    {
        VerifyAccess();
        _fontManager.VerifyServiceAccess();
    }

    /// <summary>
    /// Creates a text layout.
    /// </summary>
    /// <param name="request">The complete layout request.</param>
    /// <returns>An immutable CPU text layout.</returns>
    public TextLayout Layout(TextLayoutRequest request)
    {
        VerifyServiceAccess();
        ArgumentNullException.ThrowIfNull(request.Text);
        ArgumentNullException.ThrowIfNull(request.Font);
        var preferredFace = _fontManager.Match(request.Font);
        if (!double.IsFinite(request.FontSize) || request.FontSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "FontSize must be positive and finite.");
        if (!double.IsFinite(request.LineHeight) || request.LineHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "LineHeight must be positive and finite.");
        if (double.IsNaN(request.MaximumWidth) || request.MaximumWidth < 0)
            throw new ArgumentOutOfRangeException(nameof(request), "MaximumWidth must be non-negative or positive infinity.");
        if (request.Text.Length == 0)
            return new TextLayout(0, 0, TextBounds.Empty, []);

        using var buffer = new HbBuffer();
        using var language = new Language("und");
        var shapedLines = new List<ShapedLine>();
        foreach (var paragraph in SplitParagraphs(request.Text))
        {
            if (paragraph.Length == 0)
            {
                shapedLines.Add(CreateEmptyLine(paragraph.Start, preferredFace, request.FontSize));
                continue;
            }

            var elements = GetTextElements(request.Text, paragraph.Start, paragraph.Length);
            var resolved = ResolveElements(request.Text, elements, preferredFace);
            if (request.Wrapping == TextWrapping.NoWrap || double.IsPositiveInfinity(request.MaximumWidth))
            {
                shapedLines.Add(ShapeLine(
                    request.Text,
                    paragraph.Start,
                    paragraph.Length,
                    resolved,
                    preferredFace,
                    request.FontSize,
                    buffer,
                    language));
                continue;
            }

            var elementIndex = 0;
            while (elementIndex < elements.Count)
            {
                var breakIndex = FindLineBreak(
                    request.Text,
                    elements,
                    resolved,
                    elementIndex,
                    preferredFace,
                    request.FontSize,
                    request.MaximumWidth,
                    buffer,
                    language);
                var start = elements[elementIndex].Start;
                var end = elements[breakIndex - 1].Start + elements[breakIndex - 1].Length;
                var lineElements = elements.GetRange(elementIndex, breakIndex - elementIndex);
                shapedLines.Add(ShapeLine(
                    request.Text,
                    start,
                    end - start,
                    ResolveElements(request.Text, lineElements, preferredFace),
                    preferredFace,
                    request.FontSize,
                    buffer,
                    language));
                elementIndex = breakIndex;
            }
        }

        return PositionLines(shapedLines, request);
    }

    private static ShapedLine ShapeLine(
        string text,
        int start,
        int length,
        List<ResolvedElement> resolved,
        FontFace preferredFace,
        double fontSize,
        HbBuffer buffer,
        Language language)
    {
        var runs = new List<ShapedRun>();
        var penX = 0d;
        var index = 0;
        while (index < resolved.Count)
        {
            var first = resolved[index];
            var endIndex = index + 1;
            while (endIndex < resolved.Count &&
                   ReferenceEquals(resolved[endIndex].FontFace, first.FontFace) &&
                   resolved[endIndex].Script.Equals(first.Script) &&
                   resolved[endIndex].IsMissing == first.IsMissing)
            {
                endIndex++;
            }

            var runStart = first.Range.Start;
            var runEnd = resolved[endIndex - 1].Range.Start + resolved[endIndex - 1].Range.Length;
            var glyphs = first.IsMissing
                ? CreateMissingGlyphs(resolved, index, endIndex, ref penX, fontSize)
                : ShapeRun(text, runStart, runEnd - runStart, first.FontFace, first.Script, ref penX, fontSize, buffer, language);
            runs.Add(new ShapedRun(first.FontFace, runStart, runEnd - runStart, glyphs));
            index = endIndex;
        }

        var metricFaces = runs.Count == 0 ? [preferredFace] : runs.Select(run => run.FontFace).Distinct().ToArray();
        var ascent = metricFaces.Max(face => Scale(face.NativeFace.Ascender, face, fontSize));
        var descent = metricFaces.Max(face => Scale(face.NativeFace.Descender, face, fontSize));
        var lineGap = metricFaces.Max(face => Math.Max(0, Scale(face.NativeFace.LineGap, face, fontSize)));
        return new ShapedLine(start, length, penX, ascent, descent, lineGap, runs);
    }

    private List<ResolvedElement> ResolveElements(
        string text,
        List<TextElementRange> elements,
        FontFace preferredFace)
    {
        var scripts = ResolveScripts(elements);
        var result = new List<ResolvedElement>(elements.Count);
        for (var i = 0; i < elements.Count; i++)
        {
            var element = elements[i];
            if (element.IsUnsupported)
            {
                result.Add(new ResolvedElement(
                    element,
                    _fontManager.Match(_fontManager.DefaultFont),
                    scripts[i],
                    true));
                continue;
            }
            var fallback = _fontManager.ResolveFallback(preferredFace, text, element.Start, element.Length);
            result.Add(new ResolvedElement(element, fallback.FontFace, scripts[i], fallback.IsMissing));
        }
        return result;
    }

    private static Script[] ResolveScripts(List<TextElementRange> elements)
    {
        var scripts = elements.Select(element => element.Script).ToArray();
        for (var i = 0; i < scripts.Length; i++)
        {
            if (!scripts[i].Equals(Script.Common) && !scripts[i].Equals(Script.Inherited))
                continue;
            var replacement = i > 0 ? scripts[i - 1] : Script.Common;
            if (!replacement.Equals(Script.Common) && !replacement.Equals(Script.Inherited))
            {
                scripts[i] = replacement;
                continue;
            }
            for (var right = i + 1; right < scripts.Length; right++)
            {
                if (scripts[right].Equals(Script.Common) || scripts[right].Equals(Script.Inherited))
                    continue;
                scripts[i] = scripts[right];
                break;
            }
            if (scripts[i].Equals(Script.Common) || scripts[i].Equals(Script.Inherited))
                scripts[i] = Script.Latin;
        }
        return scripts;
    }

    private static List<ShapedGlyph> ShapeRun(
        string text,
        int start,
        int length,
        FontFace fontFace,
        Script script,
        ref double penX,
        double fontSize,
        HbBuffer buffer,
        Language language)
    {
        buffer.Reset();
        buffer.Direction = Direction.LeftToRight;
        buffer.Script = script;
        buffer.Language = language;
        buffer.ClusterLevel = ClusterLevel.MonotoneGraphemes;
        buffer.AddUtf16(text, start, length);
        fontFace.NativeFace.HarfBuzzFont.Shape(buffer);
        var infos = buffer.GlyphInfos;
        var positions = buffer.GlyphPositions;
        if (infos.Length != positions.Length)
            throw new InvalidOperationException(
                $"HarfBuzz returned {infos.Length} glyph infos and {positions.Length} glyph positions.");

        var scale = fontSize / fontFace.NativeFace.UnitsPerEm;
        var glyphs = new List<ShapedGlyph>(infos.Length);
        for (var i = 0; i < infos.Length; i++)
        {
            var position = positions[i];
            var advance = position.XAdvance * scale;
            glyphs.Add(new ShapedGlyph(
                infos[i].Codepoint,
                checked((int)infos[i].Cluster),
                penX,
                advance,
                -position.YAdvance * scale,
                position.XOffset * scale,
                -position.YOffset * scale,
                false));
            penX += advance;
        }
        return glyphs;
    }

    private static List<ShapedGlyph> CreateMissingGlyphs(
        List<ResolvedElement> elements,
        int start,
        int end,
        ref double penX,
        double fontSize)
    {
        var glyphs = new List<ShapedGlyph>(end - start);
        for (var i = start; i < end; i++)
        {
            var element = elements[i];
            var advance = Scale(
                element.FontFace.NativeFace.GetHorizontalAdvance(0),
                element.FontFace,
                fontSize);
            glyphs.Add(new ShapedGlyph(0, element.Range.Start, penX, advance, 0, 0, 0, true));
            penX += advance;
        }
        return glyphs;
    }

    private static TextLayout PositionLines(List<ShapedLine> shapedLines, TextLayoutRequest request)
    {
        var width = shapedLines.Count == 0 ? 0 : shapedLines.Max(line => line.Width);
        var y = 0d;
        var inkBounds = TextBounds.Empty;
        var lines = new TextLine[shapedLines.Count];
        for (var lineIndex = 0; lineIndex < shapedLines.Count; lineIndex++)
        {
            var source = shapedLines[lineIndex];
            var naturalHeight = source.Ascent + source.Descent + source.LineGap;
            var height = naturalHeight * request.LineHeight;
            var multiplierDifference = height - naturalHeight;
            var baseline = y + source.Ascent + source.LineGap / 2 + multiplierDifference / 2;
            var x = request.Alignment switch
            {
                TextAlignment.Center => (width - source.Width) / 2,
                TextAlignment.Right => width - source.Width,
                _ => 0
            };
            var runs = new TextGlyphRun[source.Runs.Count];
            for (var runIndex = 0; runIndex < source.Runs.Count; runIndex++)
            {
                var run = source.Runs[runIndex];
                var glyphs = new PositionedGlyph[run.Glyphs.Count];
                for (var glyphIndex = 0; glyphIndex < run.Glyphs.Count; glyphIndex++)
                {
                    var glyph = run.Glyphs[glyphIndex];
                    glyphs[glyphIndex] = new PositionedGlyph(
                        glyph.GlyphId,
                        glyph.Cluster,
                        x + glyph.X,
                        baseline,
                        glyph.XAdvance,
                        glyph.YAdvance,
                        glyph.XOffset,
                        glyph.YOffset,
                        glyph.IsMissing);
                    if (run.FontFace.NativeFace.TryGetGlyphExtents(glyph.GlyphId, out var extents))
                    {
                        var scale = request.FontSize / run.FontFace.NativeFace.UnitsPerEm;
                        var left = x + glyph.X + glyph.XOffset + extents.XBearing * scale;
                        var right = left + extents.Width * scale;
                        var top = baseline + glyph.YOffset - extents.YBearing * scale;
                        var bottom = top - extents.Height * scale;
                        var glyphBounds = new TextBounds(
                            Math.Min(left, right),
                            Math.Min(top, bottom),
                            Math.Abs(right - left),
                            Math.Abs(bottom - top));
                        inkBounds = TextBounds.Union(inkBounds, glyphBounds);
                    }
                }
                runs[runIndex] = new TextGlyphRun(run.FontFace, run.Start, run.Length, glyphs);
            }
            lines[lineIndex] = new TextLine(
                source.Start,
                source.Length,
                x,
                y,
                source.Width,
                naturalHeight,
                height,
                baseline,
                runs);
            y += height;
        }
        return new TextLayout(width, y, inkBounds, lines);
    }

    private static int FindLineBreak(
        string text,
        List<TextElementRange> elements,
        List<ResolvedElement> resolved,
        int start,
        FontFace preferredFace,
        double fontSize,
        double maximumWidth,
        HbBuffer buffer,
        Language language)
    {
        if (maximumWidth == 0)
            return start + 1;

        var lastFittingBoundary = -1;
        var lastWhitespaceBoundary = -1;
        for (var index = start; index < elements.Count; index++)
        {
            var end = index + 1;
            var lineStart = elements[start].Start;
            var lineEnd = elements[index].Start + elements[index].Length;
            var candidateElements = elements.GetRange(start, end - start);
            var candidateResolved = resolved.GetRange(start, end - start);
            var candidateScripts = ResolveScripts(candidateElements);
            for (var candidateIndex = 0; candidateIndex < candidateResolved.Count; candidateIndex++)
                candidateResolved[candidateIndex] = candidateResolved[candidateIndex] with { Script = candidateScripts[candidateIndex] };
            var width = ShapeLine(
                text,
                lineStart,
                lineEnd - lineStart,
                candidateResolved,
                preferredFace,
                fontSize,
                buffer,
                language).Width;
            if (width <= maximumWidth)
            {
                lastFittingBoundary = end;
                if (elements[index].IsBreakWhitespace)
                    lastWhitespaceBoundary = end;
            }
        }
        if (lastFittingBoundary == elements.Count)
            return elements.Count;
        if (lastWhitespaceBoundary > start)
            return lastWhitespaceBoundary;
        if (lastFittingBoundary > start)
            return lastFittingBoundary;
        return start + 1;
    }

    private static List<TextElementRange> GetTextElements(string text, int start, int length)
    {
        var value = text.Substring(start, length);
        var starts = StringInfo.ParseCombiningCharacters(value);
        var elements = new List<TextElementRange>(starts.Length);
        for (var index = 0; index < starts.Length; index++)
        {
            var localStart = starts[index];
            var localEnd = index + 1 < starts.Length ? starts[index + 1] : value.Length;
            var span = value.AsSpan(localStart, localEnd - localStart);
            var script = Script.Common;
            var unsupported = false;
            var breakWhitespace = false;
            for (var scalarIndex = 0; scalarIndex < span.Length;)
            {
                var status = Rune.DecodeFromUtf16(span[scalarIndex..], out var rune, out var consumed);
                if (status != System.Buffers.OperationStatus.Done)
                {
                    rune = Rune.ReplacementChar;
                    consumed = 1;
                }
                scalarIndex += consumed;
                var currentScript = UnicodeFunctions.Default.GetScript(rune.Value);
                if (script.Equals(Script.Common) || script.Equals(Script.Inherited))
                    script = currentScript;
                unsupported |= currentScript.HorizontalDirection == Direction.RightToLeft || IsBidiControl(rune.Value);
                breakWhitespace |= IsBreakWhitespace(rune);
            }
            elements.Add(new TextElementRange(
                start + localStart,
                localEnd - localStart,
                script,
                unsupported,
                breakWhitespace));
        }
        return elements;
    }

    private static IEnumerable<ParagraphRange> SplitParagraphs(string text)
    {
        var start = 0;
        for (var index = 0; index < text.Length; index++)
        {
            var breakLength = text[index] switch
            {
                '\r' when index + 1 < text.Length && text[index + 1] == '\n' => 2,
                '\r' or '\n' or '\u2028' or '\u2029' => 1,
                _ => 0
            };
            if (breakLength == 0)
                continue;
            yield return new ParagraphRange(start, index - start);
            index += breakLength - 1;
            start = index + 1;
        }
        yield return new ParagraphRange(start, text.Length - start);
    }

    private static ShapedLine CreateEmptyLine(int start, FontFace fontFace, double fontSize)
    {
        return new ShapedLine(
            start,
            0,
            0,
            Scale(fontFace.NativeFace.Ascender, fontFace, fontSize),
            Scale(fontFace.NativeFace.Descender, fontFace, fontSize),
            Math.Max(0, Scale(fontFace.NativeFace.LineGap, fontFace, fontSize)),
            []);
    }

    private static double Scale(int value, FontFace fontFace, double fontSize)
    {
        return value * fontSize / fontFace.NativeFace.UnitsPerEm;
    }

    private static bool IsBidiControl(int scalar)
    {
        return scalar is 0x061C or 0x200E or 0x200F or
            >= 0x202A and <= 0x202E or >= 0x2066 and <= 0x2069;
    }

    private static bool IsBreakWhitespace(Rune rune)
    {
        return Rune.IsWhiteSpace(rune) && rune.Value is not 0x00A0 and not 0x2007 and not 0x202F and not 0x2060 and not 0xFEFF;
    }

    private void VerifyAccess()
    {
        if (Environment.CurrentManagedThreadId != _ownerThreadId)
            throw new InvalidOperationException("Font services must be accessed from their owner thread.");
    }

    private readonly record struct ParagraphRange(int Start, int Length);
    private readonly record struct ResolvedElement(
        TextElementRange Range,
        FontFace FontFace,
        Script Script,
        bool IsMissing);
    private readonly record struct ShapedGlyph(
        uint GlyphId,
        int Cluster,
        double X,
        double XAdvance,
        double YAdvance,
        double XOffset,
        double YOffset,
        bool IsMissing);
    private sealed record ShapedRun(FontFace FontFace, int Start, int Length, List<ShapedGlyph> Glyphs);
    private sealed record ShapedLine(
        int Start,
        int Length,
        double Width,
        double Ascent,
        double Descent,
        double LineGap,
        List<ShapedRun> Runs);
}

using System.Text;

namespace PanguEngine.Client.UI.Styling;

/// <summary>
/// Compiles the minimum CSS grammar into an immutable <see cref="UiStyleSheet"/> using a strict one-pass scan.
/// </summary>
internal static class UiStyleParser
{
    public static UiStyleSheet Parse(string text, string? sourceName)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new Parser(text, sourceName).ParseStylesheet();
    }

    public static UiStyleSheet Parse(Stream stream, string? sourceName)
    {
        ArgumentNullException.ThrowIfNull(stream);

        string text;
        try
        {
            var bytes = ReadAllBytes(stream);
            var encoding = new UTF8Encoding(false, throwOnInvalidBytes: true);
            text = encoding.GetString(StripBom(bytes));
        }
        catch (DecoderFallbackException ex)
        {
            throw new UiStyleParseException(UiStyleParseError.InvalidEncoding, sourceName, 1, 1, 0, ex);
        }

        return new Parser(text, sourceName).ParseStylesheet();
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static byte[] StripBom(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return bytes.AsSpan(3).ToArray();
        return bytes;
    }

    private readonly struct Mark(int line, int column, int pos)
    {
        public readonly int Line = line;
        public readonly int Column = column;
        public readonly int Pos = pos;
    }

    private sealed class Parser(string text, string? sourceName)
    {
        private int _pos;
        private int _line = 1;
        private int _col = 1;

        public UiStyleSheet ParseStylesheet()
        {
            SkipTrivia();
            var rules = new List<UiStyleRule>();
            while (_pos < text.Length)
            {
                if (!IsIdentifierStart(Peek()) && Peek() is not ('*' or '.' or '#' or ':'))
                    throw Error(UiStyleParseError.TrailingToken, GetMark(), 1);
                rules.Add(ParseRule());
                SkipTrivia();
            }

            return new UiStyleSheet(rules, sourceName);
        }

        private UiStyleRule ParseRule()
        {
            var selectorStart = GetMark();
            var selectors = new List<UiStyleSelector>
            {
                ParseSelectorChain(out var selectorEnd)
            };

            while (text[_pos] == ',')
            {
                Advance();
                SkipTrivia();
                selectors.Add(ParseSelectorChain(out selectorEnd));
            }

            var selectorLength = selectorEnd.Pos - selectorStart.Pos;
            var sourceLocation = new UiStyleSourceLocation(sourceName, selectorStart.Line, selectorStart.Column, selectorLength);

            Advance();
            var declarations = new List<UiStyleRule.CssDeclaration>();
            while (true)
            {
                SkipTrivia();
                if (_pos >= text.Length)
                    throw Error(UiStyleParseError.InvalidSyntax, GetMark(), 1);
                if (text[_pos] == '}')
                {
                    Advance();
                    break;
                }

                declarations.Add(ParseDeclaration());
            }

            return UiStyleRule.FromCss(selectors, declarations, sourceLocation);
        }

        private UiStyleSelector ParseSelectorChain(out Mark selectorEnd)
        {
            var segments = new List<UiStyleSelectorSegment>();
            var combinators = new List<UiStyleCombinator>();
            selectorEnd = ParseCompoundSegment(segments);

            while (true)
            {
                var sawWhitespace = SkipTrivia();
                if (_pos >= text.Length)
                    throw Error(UiStyleParseError.InvalidSyntax, GetMark(), 1);

                var c = text[_pos];
                if (c is '{' or ',')
                    break;

                var explicitCombinator = c switch
                {
                    '>' => UiStyleCombinator.Child,
                    '+' => UiStyleCombinator.AdjacentSibling,
                    '~' => UiStyleCombinator.SubsequentSibling,
                    _ => (UiStyleCombinator?)null
                };

                if (explicitCombinator is { } combinator)
                {
                    Advance();
                    SkipTrivia();
                    selectorEnd = ParseCompoundSegment(segments);
                    combinators.Add(combinator);
                    continue;
                }

                if (!sawWhitespace || (!IsIdentifierStart(c) && c is not ('*' or '.' or '#' or ':')))
                    throw Error(UiStyleParseError.InvalidSyntax, GetMark(), 1);

                selectorEnd = ParseCompoundSegment(segments);
                combinators.Add(UiStyleCombinator.Descendant);
            }

            return UiStyleSelector.Create(segments, combinators);
        }

        private Mark ParseCompoundSegment(List<UiStyleSelectorSegment> segments)
        {
            var start = GetMark();
            var typeName = "*";
            var hasType = false;
            if (_pos < text.Length && text[_pos] == '*')
            {
                Advance();
                hasType = true;
            }
            else if (_pos < text.Length && IsIdentifierStart(text[_pos]))
            {
                typeName = ReadIdentifier();
                hasType = true;
            }

            var classes = new List<string>();
            string? id = null;
            var pseudoClasses = new List<UiPseudoClass>();
            var end = GetMark();

            while (true)
            {
                SkipComments();
                if (_pos >= text.Length)
                    break;

                var c = text[_pos];
                if (c == '.')
                {
                    Advance();
                    classes.Add(ReadIdentifier());
                }
                else if (c == '#')
                {
                    Advance();
                    var idStart = GetMark();
                    var idValue = ReadIdentifier();
                    if (id is not null)
                        throw Error(UiStyleParseError.DuplicateId, idStart, idValue.Length);
                    id = idValue;
                }
                else if (c == ':')
                {
                    Advance();
                    pseudoClasses.Add(UiPseudoClass.Get(ReadIdentifier()));
                }
                else
                {
                    break;
                }

                end = GetMark();
            }

            if (!hasType && classes.Count == 0 && id is null && pseudoClasses.Count == 0)
                throw Error(UiStyleParseError.InvalidSyntax, start, 1);

            segments.Add(UiStyleSelectorSegment.Create(null, typeName, classes, id, pseudoClasses));
            return end;
        }

        private UiStyleRule.CssDeclaration ParseDeclaration()
        {
            var propertyStart = GetMark();
            var propertyName = ReadIdentifier();
            SkipTrivia();
            if (_pos >= text.Length || text[_pos] != ':')
                throw Error(UiStyleParseError.InvalidSyntax, GetMark(), 1);
            Advance();

            var rawValue = ReadValue(out var valueStart, out var valueLength);
            if (rawValue.Length == 0)
                throw Error(UiStyleParseError.InvalidSyntax, valueStart, 0);

            if (_pos >= text.Length || text[_pos] != ';')
                throw Error(UiStyleParseError.MissingSemicolon, GetMark(), 1);
            Advance();

            return new UiStyleRule.CssDeclaration(
                propertyName,
                rawValue,
                new UiStyleSourceLocation(sourceName, propertyStart.Line, propertyStart.Column, propertyName.Length),
                new UiStyleSourceLocation(sourceName, valueStart.Line, valueStart.Column, valueLength));
        }

        private string ReadValue(out Mark start, out int sourceLength)
        {
            SkipTrivia();
            start = GetMark();
            var startPosition = _pos;
            var sourceEndPosition = startPosition;
            var builder = new StringBuilder();
            while (_pos < text.Length)
            {
                var c = text[_pos];
                if (c is ';' or '}' or '{')
                    break;

                if (c == '/' && _pos + 1 < text.Length && text[_pos + 1] == '*')
                {
                    var commentStart = GetMark();
                    Advance();
                    Advance();
                    var closed = false;
                    while (_pos < text.Length)
                    {
                        if (text[_pos] == '*' && _pos + 1 < text.Length && text[_pos + 1] == '/')
                        {
                            Advance();
                            Advance();
                            closed = true;
                            break;
                        }

                        Advance();
                    }

                    if (!closed)
                        throw Error(UiStyleParseError.UnterminatedComment, commentStart, 2);
                    if (builder.Length > 0 && !IsAsciiWhitespace(builder[^1]))
                        builder.Append(' ');
                    continue;
                }

                builder.Append(c);
                Advance();
                if (!IsAsciiWhitespace(c))
                    sourceEndPosition = _pos;
            }

            while (builder.Length > 0 && IsAsciiWhitespace(builder[^1]))
                builder.Length--;
            sourceLength = sourceEndPosition - startPosition;
            return builder.ToString();
        }

        private string ReadIdentifier()
        {
            var start = GetMark();
            if (_pos >= text.Length || !IsIdentifierStart(text[_pos]))
                throw Error(UiStyleParseError.InvalidSyntax, start, 1);

            var builder = new StringBuilder();
            while (_pos < text.Length && IsIdentifierPart(text[_pos]))
            {
                builder.Append(text[_pos]);
                Advance();
            }

            return builder.ToString();
        }

        private void SkipComments()
        {
            while (_pos + 1 < text.Length && text[_pos] == '/' && text[_pos + 1] == '*')
            {
                var start = GetMark();
                Advance();
                Advance();
                var closed = false;
                while (_pos < text.Length)
                {
                    if (text[_pos] == '*' && _pos + 1 < text.Length && text[_pos + 1] == '/')
                    {
                        Advance();
                        Advance();
                        closed = true;
                        break;
                    }

                    Advance();
                }

                if (!closed)
                    throw Error(UiStyleParseError.UnterminatedComment, start, 2);
            }
        }

        private bool SkipTrivia()
        {
            var sawWhitespace = false;
            while (_pos < text.Length)
            {
                var c = text[_pos];
                if (IsAsciiWhitespace(c))
                {
                    sawWhitespace = true;
                    Advance();
                    continue;
                }

                if (c == '/' && _pos + 1 < text.Length && text[_pos + 1] == '*')
                {
                    var start = GetMark();
                    Advance();
                    Advance();
                    var closed = false;
                    while (_pos < text.Length)
                    {
                        if (text[_pos] == '*' && _pos + 1 < text.Length && text[_pos + 1] == '/')
                        {
                            Advance();
                            Advance();
                            closed = true;
                            break;
                        }

                        Advance();
                    }

                    if (!closed)
                        throw Error(UiStyleParseError.UnterminatedComment, start, 2);
                    continue;
                }

                break;
            }

            return sawWhitespace;
        }

        private void Advance()
        {
            var c = text[_pos];
            _pos++;
            if (c == '\n')
            {
                _line++;
                _col = 1;
            }
            else if (c == '\r')
            {
                if (_pos < text.Length && text[_pos] == '\n')
                    _pos++;
                _line++;
                _col = 1;
            }
            else
            {
                _col++;
            }
        }

        private char Peek() => text[_pos];

        private Mark GetMark() => new(_line, _col, _pos);

        private UiStyleParseException Error(UiStyleParseError error, Mark mark, int length, Exception? inner = null)
            => new(error, sourceName, mark.Line, mark.Column, length, inner);

        private static bool IsAsciiWhitespace(char c) =>
            c is ' ' or '\t' or '\n' or '\r' or '\f' or '\v';

        private static bool IsIdentifierStart(char c) =>
            char.IsAsciiLetter(c) || c == '_' || c == '-';

        private static bool IsIdentifierPart(char c) =>
            char.IsAsciiLetterOrDigit(c) || c == '_' || c == '-';
    }
}

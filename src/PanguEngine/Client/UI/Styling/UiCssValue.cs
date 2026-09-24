using System.Text;

namespace PanguEngine.Client.UI.Styling;

/// <summary>Represents a CSS value containing literal text, functions and variable references.</summary>
internal sealed class UiCssValue
{
    private readonly IReadOnlyList<Part> _parts;

    private UiCssValue(IReadOnlyList<Part> parts)
    {
        _parts = parts;
        var references = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in parts)
            part.CollectReferences(references);
        References = references;
        Text = string.Concat(parts.Select(part => part.Text)).Trim();
    }

    internal string Text { get; }
    internal IReadOnlySet<string> References { get; }
    internal bool HasVariables => References.Count != 0;

    internal static UiCssValue Parse(string text, UiStyleSourceLocation location) =>
        new Parser(text, location).Parse();

    internal string Substitute(Func<string, string?> resolve, Func<string, string?>? describeFailure = null) =>
        string.Concat(_parts.Select(part => part.Substitute(resolve, describeFailure))).Trim();

    internal (UiCssValue Expression, bool IsImportant) ExtractImportant(bool allowEmpty)
    {
        const string marker = "!important";
        for (var partIndex = 0; partIndex < _parts.Count; partIndex++)
        {
            if (_parts[partIndex] is not Literal literal)
                continue;
            char quote = '\0';
            for (var index = 0; index < literal.Value.Length; index++)
            {
                var current = literal.Value[index];
                if (quote != '\0')
                {
                    if (current == '\\')
                        index++;
                    else if (current == quote)
                        quote = '\0';
                    continue;
                }
                if (current is '\'' or '"')
                {
                    quote = current;
                    continue;
                }
                if (current != '!' || !literal.Value.AsSpan(index).StartsWith(marker, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (partIndex != _parts.Count - 1 || index + marker.Length != literal.Value.TrimEnd().Length)
                    throw new FormatException("The !important marker must appear exactly once at the end of a CSS value.");
                var parts = _parts.Take(partIndex).Append(new Literal(literal.Value[..index])).ToArray();
                var expression = new UiCssValue(parts);
                if (!allowEmpty && expression.Text.Length == 0)
                    throw new FormatException("The !important marker requires a non-empty CSS value.");
                return (expression, true);
            }
        }
        return (this, false);
    }

    private abstract record Part
    {
        internal abstract string Text { get; }
        internal abstract string Substitute(Func<string, string?> resolve, Func<string, string?>? describeFailure);
        internal virtual void CollectReferences(HashSet<string> references) { }
    }

    private sealed record Literal(string Value) : Part
    {
        internal override string Text => Value;
        internal override string Substitute(Func<string, string?> resolve, Func<string, string?>? describeFailure) => Value;
    }

    private sealed record Function(string Name, UiCssValue Arguments) : Part
    {
        internal override string Text => Name + "(" + string.Concat(Arguments._parts.Select(part => part.Text)) + ")";
        internal override string Substitute(Func<string, string?> resolve, Func<string, string?>? describeFailure) =>
            Name + "(" + string.Concat(Arguments._parts.Select(part => part.Substitute(resolve, describeFailure))) + ")";
        internal override void CollectReferences(HashSet<string> references) => references.UnionWith(Arguments.References);
    }

    private sealed record Variable(string Name, UiCssValue? Fallback) : Part
    {
        internal override string Text => "var(" + Name + (Fallback is null ? "" : "," + Fallback.Text) + ")";
        internal override string Substitute(Func<string, string?> resolve, Func<string, string?>? describeFailure)
        {
            var value = resolve(Name);
            if (value is null)
            {
                if (Fallback is null)
                    throw new FormatException($"CSS variable '{Name}' is missing or invalid and has no fallback." +
                        (describeFailure?.Invoke(Name) is { } detail ? " " + detail : ""));
                value = Fallback.Substitute(resolve, describeFailure);
            }
            return " " + value + " ";
        }

        internal override void CollectReferences(HashSet<string> references)
        {
            references.Add(Name);
            if (Fallback is not null)
                references.UnionWith(Fallback.References);
        }
    }

    private sealed class Parser(string text, UiStyleSourceLocation location)
    {
        private int _position;

        internal UiCssValue Parse() => ReadSequence(false);

        private UiCssValue ReadSequence(bool nested)
        {
            var parts = new List<Part>();
            var literal = new StringBuilder();
            while (_position < text.Length)
            {
                var current = text[_position];
                if (current == ')')
                {
                    if (!nested)
                        throw Error(_position);
                    _position++;
                    Flush();
                    return new UiCssValue(parts.ToArray());
                }
                if (current is '{' or '}' or '\\')
                    throw Error(_position);
                if (SkipComment())
                {
                    literal.Append(' ');
                    continue;
                }
                if (current is '\'' or '"')
                {
                    literal.Append(ReadString());
                    continue;
                }
                if (IsNamePart(current) || current == '#')
                {
                    var start = _position++;
                    while (_position < text.Length && IsNamePart(text[_position]))
                        _position++;
                    var name = text[start.._position];
                    if (_position < text.Length && text[_position] == '(')
                    {
                        _position++;
                        Flush();
                        parts.Add(name.Equals("var", StringComparison.OrdinalIgnoreCase)
                            ? ReadVariable()
                            : new Function(name, ReadSequence(true)));
                    }
                    else
                        literal.Append(name);
                    continue;
                }
                if (current == '(')
                {
                    _position++;
                    Flush();
                    parts.Add(new Function("", ReadSequence(true)));
                    continue;
                }
                literal.Append(current);
                _position++;
            }
            if (nested)
                throw Error(_position);
            Flush();
            return new UiCssValue(parts.ToArray());

            void Flush()
            {
                if (literal.Length == 0)
                    return;
                parts.Add(new Literal(literal.ToString()));
                literal.Clear();
            }
        }

        private Variable ReadVariable()
        {
            SkipTrivia();
            var start = _position;
            while (_position < text.Length && IsNamePart(text[_position]))
                _position++;
            var name = text[start.._position];
            if (name.Length <= 2 || !name.StartsWith("--", StringComparison.Ordinal))
                throw Error(start);
            SkipTrivia();
            if (_position >= text.Length)
                throw Error(_position);
            if (text[_position] == ')')
            {
                _position++;
                return new Variable(name, null);
            }
            if (text[_position] != ',')
                throw Error(_position);
            _position++;
            return new Variable(name, ReadSequence(true));
        }

        private string ReadString()
        {
            var start = _position;
            var quote = text[_position++];
            while (_position < text.Length)
            {
                var current = text[_position++];
                if (current == quote)
                    return text[start.._position];
                if (current == '\\' && _position < text.Length)
                {
                    if (text[_position++] == '\r' && _position < text.Length && text[_position] == '\n')
                        _position++;
                }
                else if (current is '\r' or '\n')
                    throw Error(_position - 1);
            }
            throw Error(start);
        }

        private bool SkipComment()
        {
            if (_position + 1 >= text.Length || text[_position] != '/' || text[_position + 1] != '*')
                return false;
            var start = _position;
            var end = text.IndexOf("*/", _position + 2, StringComparison.Ordinal);
            if (end < 0)
                throw Error(start, UiStyleParseError.UnterminatedComment);
            _position = end + 2;
            return true;
        }

        private void SkipTrivia()
        {
            while (_position < text.Length)
            {
                if (char.IsWhiteSpace(text[_position]))
                    _position++;
                else if (!SkipComment())
                    break;
            }
        }

        private UiStyleParseException Error(int offset, UiStyleParseError error = UiStyleParseError.InvalidSyntax)
        {
            var line = location.Line;
            var column = location.Column;
            for (var index = 0; index < offset; index++)
            {
                if (text[index] == '\r')
                {
                    if (index + 1 < offset && text[index + 1] == '\n')
                        index++;
                    line++;
                    column = 1;
                }
                else if (text[index] == '\n')
                {
                    line++;
                    column = 1;
                }
                else
                    column++;
            }
            return new UiStyleParseException(error, location.SourceName, line, column, offset < text.Length ? 1 : 0);
        }

        private static bool IsNamePart(char value) => char.IsAsciiLetterOrDigit(value) || value is '_' or '-';
    }
}

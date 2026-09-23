using System.Globalization;

namespace PanguEngine.Client.UI.Drawing.Geometry;

internal sealed class SvgPathParser
{
    private readonly string _text;
    private readonly List<PathSegment> _segments = [];
    private int _position;
    private char _command;
    private Point _current;
    private Point _subpathStart;
    private Point _lastControl;
    private bool _hasLastControl;
    private char _previous;

    private SvgPathParser(string text) => _text = text;

    internal static PathSegment[] Parse(string? data)
    {
        if (string.IsNullOrWhiteSpace(data))
            return [];

        var parser = new SvgPathParser(data);
        try
        {
            return parser.ParseCore();
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new FormatException(
                $"SVG path error at offset {parser._position}: Geometry exceeds the finite coordinate range.",
                exception);
        }
    }

    private PathSegment[] ParseCore()
    {
        SkipSeparators();
        if (_position < _text.Length && _text[_position] is not ('M' or 'm'))
            Error("Path must begin with moveto.");
        while (_position < _text.Length)
        {
            if (char.IsLetter(_text[_position]))
                _command = _text[_position++];
            else if (_command == '\0')
                Error("Expected a path command after closepath.");
            ParseCommand(_command);
            SkipSeparators();
        }
        return _segments.ToArray();
    }

    private void ParseCommand(char command)
    {
        var relative = char.IsLower(command);
        var upper = char.ToUpperInvariant(command);
        if (upper == 'Z')
        {
            Add(PathSegmentFactory.Close(_current, _subpathStart));
            _hasLastControl = false;
            _command = '\0';
            _previous = 'Z';
            return;
        }

        var first = true;
        SkipSeparators();
        if (_position < _text.Length && _text[_position] == ',') Error("Unexpected comma.");
        while (HasNumber())
        {
            switch (upper)
            {
                case 'M':
                    var move = Pair(relative);
                    if (!first) Add(PathSegmentFactory.Line(_current, move));
                    else { Add(PathSegmentFactory.Move(move)); _subpathStart = move; }
                    first = false; upper = 'L'; _hasLastControl = false; break;
                case 'L': Add(PathSegmentFactory.Line(_current, Pair(relative))); _hasLastControl = false; break;
                case 'H': Add(PathSegmentFactory.Line(_current, new Point(relative ? _current.X + Number() : Number(), _current.Y))); _hasLastControl = false; break;
                case 'V': Add(PathSegmentFactory.Line(_current, new Point(_current.X, relative ? _current.Y + Number() : Number()))); _hasLastControl = false; break;
                case 'Q': var q = Pair(relative); var qe = Pair(relative); Add(PathSegmentFactory.Quadratic(_current, q, qe)); _lastControl = q; _hasLastControl = true; break;
                case 'T': var tc = _hasLastControl && _previous is 'Q' or 'T' ? Reflect(_lastControl) : _current; var te = Pair(relative); Add(PathSegmentFactory.Quadratic(_current, tc, te)); _lastControl = tc; _hasLastControl = true; break;
                case 'C': var c1 = Pair(relative); var c2 = Pair(relative); var ce = Pair(relative); Add(PathSegmentFactory.Cubic(_current, c1, c2, ce)); _lastControl = c2; _hasLastControl = true; break;
                case 'S': var sc1 = _hasLastControl && _previous is 'C' or 'S' ? Reflect(_lastControl) : _current; var sc2 = Pair(relative); var se = Pair(relative); Add(PathSegmentFactory.Cubic(_current, sc1, sc2, se)); _lastControl = sc2; _hasLastControl = true; break;
                case 'A': ParseArc(relative); _hasLastControl = false; break;
                default: Error($"Unsupported command '{command}'."); break;
            }
            first = false;
            _previous = upper;
            SkipSeparators();
            if (_position < _text.Length && _text[_position] == ',')
            {
                _position++;
                if (!HasNumber()) Error("Expected parameters after comma.");
            }
        }
        if (first) Error("Command has no parameters.");
    }

    private void ParseArc(bool relative)
    {
        var radiusX = Number();
        var radiusY = Number();
        var rotation = Number();
        var large = Flag();
        var sweep = Flag();
        var end = Pair(relative);
        var segment = PathSegmentFactory.Arc(_current, end, radiusX, radiusY, rotation, large != 0, sweep != 0);
        if (segment is { } value)
            Add(value);
        else
            _current = end;
    }

    private Point Reflect(Point point) => new(2 * _current.X - point.X, 2 * _current.Y - point.Y);

    private Point Pair(bool relative)
    {
        var x = Number();
        var y = Number();
        return relative ? new Point(_current.X + x, _current.Y + y) : new Point(x, y);
    }

    private int Flag()
    {
        SkipArgumentSeparator();
        if (_position >= _text.Length || _text[_position] is not ('0' or '1'))
            Error("Arc flags must be 0 or 1.");
        return _text[_position++] - '0';
    }

    private double Number()
    {
        SkipArgumentSeparator();
        var start = _position;
        if (_position < _text.Length && (_text[_position] is '+' or '-')) _position++;
        var digits = false;
        while (_position < _text.Length && char.IsDigit(_text[_position])) { _position++; digits = true; }
        if (_position < _text.Length && _text[_position] == '.') { _position++; while (_position < _text.Length && char.IsDigit(_text[_position])) { _position++; digits = true; } }
        if (!digits) Error("Expected number.");
        if (_position < _text.Length && (_text[_position] is 'e' or 'E')) { _position++; if (_position < _text.Length && (_text[_position] is '+' or '-')) _position++; var exponent = _position; while (_position < _text.Length && char.IsDigit(_text[_position])) _position++; if (exponent == _position) Error("Invalid exponent."); }
        if (!double.TryParse(_text.AsSpan(start, _position - start), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value)) Error("Number is not finite.");
        return value;
    }

    private bool HasNumber()
    {
        SkipSeparators();
        return _position < _text.Length && (_text[_position] is '+' or '-' or '.' || char.IsDigit(_text[_position]));
    }

    private void SkipSeparators()
    {
        while (_position < _text.Length && _text[_position] is ' ' or '\t' or '\r' or '\n' or '\f') _position++;
    }

    private void SkipArgumentSeparator()
    {
        SkipSeparators();
        if (_position < _text.Length && _text[_position] == ',') { _position++; SkipSeparators(); }
    }

    private void Add(PathSegment segment)
    {
        _segments.Add(segment);
        _current = segment.End;
    }

    private void Error(string message) => throw new FormatException($"SVG path error at offset {_position}: {message}");
}

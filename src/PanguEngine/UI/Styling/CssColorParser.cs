using System.Globalization;
using PanguEngine.UI.Drawing;

namespace PanguEngine.UI.Styling;

/// <summary>Converts supported absolute CSS colors into non-premultiplied sRGB colors.</summary>
internal static class CssColorParser
{
    /// <summary>Parses a trimmed standard named color, hexadecimal color, or RGB or HSL function.</summary>
    internal static Color Parse(string value)
    {
        if (Color.TryFromName(value, out var named))
            return named;
        if (value.Length > 0 && value[0] == '#')
            return Color.TryFromHex(value, out var hex) ? hex : throw InvalidColor(value);

        var opening = value.IndexOf('(');
        if (opening < 0 || value[^1] != ')')
            throw InvalidColor(value);
        var name = value.AsSpan(0, opening);
        var isRgb = name.Equals("rgb", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("rgba", StringComparison.OrdinalIgnoreCase);
        var isHsl = name.Equals("hsl", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("hsla", StringComparison.OrdinalIgnoreCase);
        if (!isRgb && !isHsl)
            throw InvalidColor(value);

        var arguments = value.AsSpan(opening + 1, value.Length - opening - 2);
        var legacy = arguments.IndexOf(',') >= 0;
        var reader = new ComponentReader(arguments, value);
        var first = reader.ReadComponent();
        reader.ReadSeparator(legacy);
        var second = reader.ReadComponent();
        reader.ReadSeparator(legacy);
        var third = reader.ReadComponent();
        reader.SkipWhitespace();
        var alpha = (byte)255;
        if (!reader.IsAtEnd)
        {
            if (!reader.TryConsume(legacy ? ',' : '/'))
                throw InvalidColor(value);
            alpha = Color.QuantizeChannel(ReadAlpha(reader.ReadComponent(), value) * 255);
            reader.SkipWhitespace();
        }
        if (!reader.IsAtEnd)
            throw InvalidColor(value);

        if (isHsl)
        {
            return Color.FromNormalizedHsl(
                ReadHue(first, value),
                ReadPercentage(second, value),
                ReadPercentage(third, value),
                alpha);
        }

        if (legacy && (first.Unit != second.Unit || second.Unit != third.Unit))
            throw InvalidColor(value);
        return new Color(ReadRgb(first, value), ReadRgb(second, value), ReadRgb(third, value), alpha);
    }

    /// <summary>Converts a numeric or percentage RGB component into a byte channel.</summary>
    private static byte ReadRgb(Component component, string original) => component.Unit switch
    {
        Unit.Number => Color.QuantizeChannel(component.Value),
        Unit.Percent => Color.QuantizeChannel(Math.Clamp(component.Value, 0, 100) / 100 * 255),
        _ => throw InvalidColor(original)
    };

    /// <summary>Converts an alpha component into a clamped coverage fraction.</summary>
    private static double ReadAlpha(Component component, string original) => component.Unit switch
    {
        Unit.Percent => Math.Clamp(component.Value, 0, 100) / 100,
        Unit.Number => Math.Clamp(component.Value, 0, 1),
        _ => throw InvalidColor(original)
    };

    /// <summary>Reads a required percentage component clamped to the range from zero to one hundred.</summary>
    private static double ReadPercentage(Component component, string original) =>
        component.Unit == Unit.Percent ? Math.Clamp(component.Value, 0, 100) : throw InvalidColor(original);

    /// <summary>Converts a supported hue component into a periodic angle in degrees.</summary>
    private static double ReadHue(Component component, string original)
    {
        var period = component.Unit switch
        {
            Unit.Number or Unit.Degrees => 360d,
            Unit.Radians => Math.Tau,
            Unit.Gradians => 400d,
            Unit.Turns => 1d,
            _ => throw InvalidColor(original)
        };
        var remainder = component.Value % period;
        if (remainder < 0)
            remainder += period;
        return component.Unit is Unit.Number or Unit.Degrees ? remainder : remainder / period * 360;
    }

    /// <summary>Creates an error for an unsupported or malformed color.</summary>
    private static FormatException InvalidColor(string value) => new($"Color '{value}' is not a supported CSS color.");

    /// <summary>Identifies the supported units of a color component.</summary>
    private enum Unit { Number, Percent, Degrees, Radians, Gradians, Turns }

    /// <summary>Represents a finite color component and its unit.</summary>
    private readonly record struct Component(double Value, Unit Unit);

    /// <summary>Reads numeric components and separators from a CSS color function.</summary>
    private ref struct ComponentReader(ReadOnlySpan<char> text, string original)
    {
        private readonly ReadOnlySpan<char> _text = text;
        private readonly string _original = original;
        private int _position;

        /// <summary>Gets whether the function arguments have been consumed.</summary>
        internal bool IsAtEnd => _position == _text.Length;

        /// <summary>Reads one finite numeric component with an optional unit.</summary>
        internal Component ReadComponent()
        {
            SkipWhitespace();
            var start = _position;
            if (!TryConsume('+'))
                TryConsume('-');
            var hasInteger = ReadDigits();
            if (TryConsume('.'))
            {
                if (!ReadDigits())
                    throw InvalidColor(_original);
            }
            else if (!hasInteger)
                throw InvalidColor(_original);
            if (TryConsume('e') || TryConsume('E'))
            {
                if (!TryConsume('+'))
                    TryConsume('-');
                if (!ReadDigits())
                    throw InvalidColor(_original);
            }

            if (!double.TryParse(
                    _text[start.._position],
                    NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent,
                    CultureInfo.InvariantCulture,
                    out var number) || !double.IsFinite(number))
            {
                throw InvalidColor(_original);
            }

            if (TryConsume('%'))
                return new Component(number, Unit.Percent);
            var unitStart = _position;
            while (_position < _text.Length && char.IsAsciiLetter(_text[_position]))
                _position++;
            var unit = _text[unitStart.._position];
            if (unit.IsEmpty)
                return new Component(number, Unit.Number);
            if (unit.Equals("deg", StringComparison.OrdinalIgnoreCase))
                return new Component(number, Unit.Degrees);
            if (unit.Equals("rad", StringComparison.OrdinalIgnoreCase))
                return new Component(number, Unit.Radians);
            if (unit.Equals("grad", StringComparison.OrdinalIgnoreCase))
                return new Component(number, Unit.Gradians);
            if (unit.Equals("turn", StringComparison.OrdinalIgnoreCase))
                return new Component(number, Unit.Turns);
            throw InvalidColor(_original);
        }

        /// <summary>Reads the required separator between two color components.</summary>
        internal void ReadSeparator(bool legacy)
        {
            var hasWhitespace = SkipWhitespace();
            if (legacy ? !TryConsume(',') : !hasWhitespace)
                throw InvalidColor(_original);
        }

        /// <summary>Consumes CSS whitespace and reports whether any was present.</summary>
        internal bool SkipWhitespace()
        {
            var start = _position;
            while (_position < _text.Length && _text[_position] is ' ' or '\t' or '\n' or '\r' or '\f')
                _position++;
            return _position != start;
        }

        /// <summary>Consumes the supplied character when it is next in the input.</summary>
        internal bool TryConsume(char value)
        {
            if (_position == _text.Length || _text[_position] != value)
                return false;
            _position++;
            return true;
        }

        /// <summary>Consumes ASCII digits and reports whether any were present.</summary>
        private bool ReadDigits()
        {
            var start = _position;
            while (_position < _text.Length && char.IsAsciiDigit(_text[_position]))
                _position++;
            return _position != start;
        }
    }
}

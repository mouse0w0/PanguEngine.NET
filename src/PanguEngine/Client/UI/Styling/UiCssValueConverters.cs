using System.Globalization;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Graphics.Text;

namespace PanguEngine.Client.UI.Styling;

/// <summary>
/// Provides the built-in CSS text converters that control authors select explicitly when registering
/// <see cref="UiCssRegistry"/> definitions.
/// </summary>
public static class UiCssValueConverters
{
    private static readonly Dictionary<string, HorizontalAlignment> HorizontalAlignmentMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["left"] = HorizontalAlignment.Left,
        ["center"] = HorizontalAlignment.Center,
        ["right"] = HorizontalAlignment.Right,
        ["stretch"] = HorizontalAlignment.Stretch
    };

    private static readonly Dictionary<string, VerticalAlignment> VerticalAlignmentMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["top"] = VerticalAlignment.Top,
        ["center"] = VerticalAlignment.Center,
        ["bottom"] = VerticalAlignment.Bottom,
        ["stretch"] = VerticalAlignment.Stretch
    };

    private static readonly Dictionary<string, Visibility> VisibilityMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["visible"] = Visibility.Visible,
        ["hidden"] = Visibility.Hidden,
        ["collapsed"] = Visibility.Collapsed
    };

    private static readonly Dictionary<string, Orientation> OrientationMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["horizontal"] = Orientation.Horizontal,
        ["vertical"] = Orientation.Vertical
    };

    private static readonly Dictionary<string, TextWrapping> TextWrappingMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["nowrap"] = TextWrapping.NoWrap,
        ["wrap"] = TextWrapping.Wrap
    };

    private static readonly Dictionary<string, TextAlignment> TextAlignmentMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["left"] = TextAlignment.Left,
        ["center"] = TextAlignment.Center,
        ["right"] = TextAlignment.Right
    };

    private static readonly Dictionary<string, ImageStretch> ImageStretchMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["none"] = ImageStretch.None,
        ["fill"] = ImageStretch.Fill,
        ["uniform"] = ImageStretch.Uniform,
        ["uniformtofill"] = ImageStretch.UniformToFill
    };

    private static readonly Dictionary<string, ImageSamplingMode> ImageSamplingModeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["nearest"] = ImageSamplingMode.Nearest,
        ["linear"] = ImageSamplingMode.Linear
    };

    /// <summary>Converts a trimmed CSS value into a finite logical length, accepting an optional px suffix.</summary>
    /// <param name="value">The trimmed CSS value.</param>
    /// <returns>The finite logical length.</returns>
    /// <exception cref="FormatException">Thrown when the value is not a finite decimal number.</exception>
    public static double ParseLength(string value) => ParseFinite(StripOptionalPx(value), value);

    /// <summary>Converts a trimmed CSS value into a finite unitless number.</summary>
    /// <param name="value">The trimmed CSS value.</param>
    /// <returns>The finite number.</returns>
    /// <exception cref="FormatException">Thrown when the value is not a finite decimal number or carries a unit.</exception>
    public static double ParseNumber(string value) => ParseFinite(value, value);

    /// <summary>Converts a trimmed CSS value into a thickness with one, two, three, or four edges.</summary>
    /// <param name="value">The trimmed CSS value, ordered top, right, bottom, left.</param>
    /// <returns>The parsed thickness.</returns>
    /// <exception cref="FormatException">Thrown when the value does not contain one to four lengths.</exception>
    public static Thickness ParseThickness(string value)
    {
        var parts = SplitAsciiWhitespace(value);
        if (parts.Length is < 1 or > 4)
            throw new FormatException($"Thickness '{value}' must have 1, 2, 3 or 4 values.");

        var numbers = new double[parts.Length];
        for (var i = 0; i < parts.Length; i++)
            numbers[i] = ParseLength(parts[i]);

        return parts.Length switch
        {
            1 => new Thickness(numbers[0]),
            2 => new Thickness(numbers[1], numbers[0], numbers[1], numbers[0]),
            3 => new Thickness(numbers[1], numbers[0], numbers[1], numbers[2]),
            _ => new Thickness(numbers[3], numbers[0], numbers[1], numbers[2])
        };
    }

    /// <summary>Converts a trimmed CSS value into a non-premultiplied color.</summary>
    /// <param name="value">The trimmed CSS value, either transparent or a #RRGGBB or #RRGGBBAA hex color.</param>
    /// <returns>The parsed color.</returns>
    /// <exception cref="FormatException">Thrown when the value is not a supported color.</exception>
    public static Color ParseColor(string value)
    {
        if (value.Equals("transparent", StringComparison.OrdinalIgnoreCase))
            return new Color(0, 0, 0, 0);
        if (value.Length is not (7 or 9) || value[0] != '#')
            throw new FormatException($"Color '{value}' must be #RRGGBB or #RRGGBBAA.");
        var hex = value.AsSpan(1);
        if (!IsHex(hex))
            throw new FormatException($"Color '{value}' contains an invalid hex digit.");

        var r = ToByte(hex, 0);
        var g = ToByte(hex, 2);
        var b = ToByte(hex, 4);
        var a = hex.Length == 8 ? ToByte(hex, 6) : (byte)255;
        return new Color(r, g, b, a);
    }

    /// <summary>Converts a trimmed CSS color value into a solid color brush.</summary>
    /// <param name="value">The trimmed CSS color value.</param>
    /// <returns>A solid color brush with the parsed color.</returns>
    /// <exception cref="FormatException">Thrown when the value is not a supported color.</exception>
    public static Brush ParseBrush(string value) => new SolidColorBrush(ParseColor(value));

    /// <summary>Converts a trimmed CSS value into a boolean.</summary>
    /// <param name="value">The trimmed CSS value, either true or false.</param>
    /// <returns>The parsed boolean.</returns>
    /// <exception cref="FormatException">Thrown when the value is not a boolean.</exception>
    public static bool ParseBool(string value)
    {
        if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
            return false;
        throw new FormatException($"Value '{value}' is not a boolean.");
    }

    /// <summary>Converts a trimmed CSS value into a horizontal alignment.</summary>
    /// <param name="value">The trimmed CSS value.</param>
    /// <returns>The parsed enumeration member.</returns>
    /// <exception cref="FormatException">Thrown when the value is not a recognized member.</exception>
    public static HorizontalAlignment ParseHorizontalAlignment(string value) => ParseEnum(value, HorizontalAlignmentMap);

    /// <summary>Converts a trimmed CSS value into a vertical alignment.</summary>
    /// <param name="value">The trimmed CSS value.</param>
    /// <returns>The parsed enumeration member.</returns>
    /// <exception cref="FormatException">Thrown when the value is not a recognized member.</exception>
    public static VerticalAlignment ParseVerticalAlignment(string value) => ParseEnum(value, VerticalAlignmentMap);

    /// <summary>Converts a trimmed CSS value into a visibility.</summary>
    /// <param name="value">The trimmed CSS value.</param>
    /// <returns>The parsed enumeration member.</returns>
    /// <exception cref="FormatException">Thrown when the value is not a recognized member.</exception>
    public static Visibility ParseVisibility(string value) => ParseEnum(value, VisibilityMap);

    /// <summary>Converts a trimmed CSS value into an orientation.</summary>
    /// <param name="value">The trimmed CSS value.</param>
    /// <returns>The parsed enumeration member.</returns>
    /// <exception cref="FormatException">Thrown when the value is not a recognized member.</exception>
    public static Orientation ParseOrientation(string value) => ParseEnum(value, OrientationMap);

    /// <summary>Converts a trimmed CSS value into a text wrapping mode.</summary>
    /// <param name="value">The trimmed CSS value.</param>
    /// <returns>The parsed enumeration member.</returns>
    /// <exception cref="FormatException">Thrown when the value is not a recognized member.</exception>
    public static TextWrapping ParseTextWrapping(string value) => ParseEnum(value, TextWrappingMap);

    /// <summary>Converts a trimmed CSS value into a text alignment.</summary>
    /// <param name="value">The trimmed CSS value.</param>
    /// <returns>The parsed enumeration member.</returns>
    /// <exception cref="FormatException">Thrown when the value is not a recognized member.</exception>
    public static TextAlignment ParseTextAlignment(string value) => ParseEnum(value, TextAlignmentMap);

    /// <summary>Converts a trimmed CSS value into an image stretch mode.</summary>
    /// <param name="value">The trimmed CSS value.</param>
    /// <returns>The parsed enumeration member.</returns>
    /// <exception cref="FormatException">Thrown when the value is not a recognized member.</exception>
    public static ImageStretch ParseImageStretch(string value) => ParseEnum(value, ImageStretchMap);

    /// <summary>Converts a trimmed CSS value into an image sampling mode.</summary>
    /// <param name="value">The trimmed CSS value.</param>
    /// <returns>The parsed enumeration member.</returns>
    /// <exception cref="FormatException">Thrown when the value is not a recognized member.</exception>
    public static ImageSamplingMode ParseImageSamplingMode(string value) => ParseEnum(value, ImageSamplingModeMap);

    private static double ParseFinite(string text, string original)
    {
        if (!double.TryParse(
                text,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var result)
            || !double.IsFinite(result))
        {
            throw new FormatException($"Value '{original}' is not a finite decimal number.");
        }

        return result;
    }

    private static T ParseEnum<T>(string value, IReadOnlyDictionary<string, T> map)
    {
        if (map.TryGetValue(value, out var result))
            return result;
        throw new FormatException($"Value '{value}' is not a recognized enum member.");
    }

    private static string StripOptionalPx(string value)
    {
        if (value.Length > 2 && value.EndsWith("px", StringComparison.OrdinalIgnoreCase))
            return value[..^2];
        return value;
    }

    private static string[] SplitAsciiWhitespace(string value)
    {
        var parts = new List<string>();
        var start = -1;
        for (var index = 0; index <= value.Length; index++)
        {
            if (index < value.Length && !IsAsciiWhitespace(value[index]))
            {
                if (start < 0)
                    start = index;
                continue;
            }

            if (start >= 0)
            {
                parts.Add(value[start..index]);
                start = -1;
            }
        }

        return parts.ToArray();
    }

    private static bool IsAsciiWhitespace(char value) =>
        value is ' ' or '\t' or '\n' or '\r' or '\f' or '\v';

    private static bool IsHex(ReadOnlySpan<char> hex)
    {
        foreach (var c in hex)
        {
            if (!char.IsAsciiHexDigit(c))
                return false;
        }

        return true;
    }

    private static byte ToByte(ReadOnlySpan<char> hex, int index)
    {
        var high = HexValue(hex[index]);
        var low = HexValue(hex[index + 1]);
        return (byte)((high << 4) | low);
    }

    private static int HexValue(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'A' and <= 'F' => c - 'A' + 10,
        >= 'a' and <= 'f' => c - 'a' + 10,
        _ => throw new FormatException("Invalid hex digit.")
    };
}

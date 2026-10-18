using System.Globalization;
using PanguEngine.Graphics.Text;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Drawing;

namespace PanguEngine.UI.Styling;

/// <summary>
/// Provides the built-in CSS text converters that control authors select explicitly when registering
/// <see cref="CssRegistry"/> definitions.
/// </summary>
public static class CssValueConverters
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

    private static readonly Dictionary<string, Dock> DockMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["left"] = Dock.Left,
        ["top"] = Dock.Top,
        ["right"] = Dock.Right,
        ["bottom"] = Dock.Bottom
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

    /// <summary>Converts a trimmed CSS length or auto into a requested dimension.</summary>
    /// <param name="value">The trimmed CSS value, either auto or a length with an optional px suffix.</param>
    /// <returns>NaN for automatic sizing, or the finite logical length.</returns>
    /// <exception cref="FormatException">Thrown when the value is neither auto nor a finite decimal length.</exception>
    public static double ParseDimension(string value) =>
        value.Equals("auto", StringComparison.OrdinalIgnoreCase)
            ? double.NaN
            : ParseLength(value);

    /// <summary>Converts a trimmed CSS length or none into a maximum dimension.</summary>
    /// <param name="value">The trimmed CSS value, either none or a length with an optional px suffix.</param>
    /// <returns>Positive infinity for no upper limit, or the finite logical length.</returns>
    /// <exception cref="FormatException">Thrown when the value is neither none nor a finite decimal length.</exception>
    public static double ParseMaxDimension(string value) =>
        value.Equals("none", StringComparison.OrdinalIgnoreCase)
            ? double.PositiveInfinity
            : ParseLength(value);

    /// <summary>Converts a trimmed CSS value into a finite unitless number.</summary>
    /// <param name="value">The trimmed CSS value.</param>
    /// <returns>The finite number.</returns>
    /// <exception cref="FormatException">Thrown when the value is not a finite decimal number or carries a unit.</exception>
    public static double ParseNumber(string value) => ParseFinite(value, value);

    /// <summary>Converts one to four CSS lengths into clockwise corner radii.</summary>
    /// <param name="value">The corner lengths starting at the top left.</param>
    /// <returns>The expanded corner radii.</returns>
    /// <exception cref="FormatException">The value is not one to four lengths.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A radius is negative.</exception>
    public static CornerRadius ParseCornerRadius(string value)
    {
        var parts = SplitAsciiWhitespace(value);
        if (parts.Length is < 1 or > 4)
            throw new FormatException($"Corner radius '{value}' must have 1, 2, 3 or 4 values.");
        var a = ParseLength(parts[0]);
        var b = parts.Length > 1 ? ParseLength(parts[1]) : a;
        var c = parts.Length > 2 ? ParseLength(parts[2]) : a;
        var d = parts.Length > 3 ? ParseLength(parts[3]) : b;
        return new CornerRadius(a, b, c, d);
    }

    /// <summary>Converts a trimmed CSS value into a unitless 32-bit decimal integer.</summary>
    /// <param name="value">The trimmed CSS value.</param>
    /// <returns>The parsed integer.</returns>
    /// <exception cref="FormatException">Thrown when the value is not a 32-bit decimal integer or carries a unit.</exception>
    public static int ParseInteger(string value)
    {
        if (int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var result))
            return result;

        throw new FormatException($"Value '{value}' is not a 32-bit decimal integer.");
    }

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
    /// <param name="value">The trimmed CSS value: a standard named color, transparent, a short or long hex color, or an RGB or HSL function.</param>
    /// <returns>The parsed color.</returns>
    /// <exception cref="FormatException">Thrown when the value is not a supported color.</exception>
    public static Color ParseColor(string value) => CssColorParser.Parse(value);

    /// <summary>Converts a trimmed CSS color or none into a brush.</summary>
    /// <param name="value">The trimmed CSS value: none, a standard named color, transparent, a hex color, or an RGB or HSL function.</param>
    /// <returns>Null for none, or a solid color brush with the parsed color.</returns>
    /// <exception cref="FormatException">Thrown when the value is neither none nor a supported color.</exception>
    public static Brush? ParseBrush(string value) =>
        value.Equals("none", StringComparison.OrdinalIgnoreCase)
            ? null
            : new SolidColorBrush(ParseColor(value));

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

    /// <summary>Converts a trimmed CSS value into a dock direction.</summary>
    /// <param name="value">The trimmed CSS value, either left, top, right, or bottom.</param>
    /// <returns>The parsed dock direction.</returns>
    /// <exception cref="FormatException">Thrown when the value is not a recognized dock direction.</exception>
    public static Dock ParseDock(string value) => ParseEnum(value, DockMap);

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
}

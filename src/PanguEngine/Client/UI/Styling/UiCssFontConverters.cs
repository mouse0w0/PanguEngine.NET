using System.Text;
using PanguEngine.Graphics.Text;

namespace PanguEngine.Client.UI.Styling;

internal static class UiCssFontConverters
{
    internal static string ParseFamily(string value)
    {
        value = value.Trim();
        if (value.Length == 0)
            throw new FormatException("A font family is required.");
        if (value[0] is '\'' or '"')
        {
            var quote = value[0];
            var result = new StringBuilder();
            for (var index = 1; index < value.Length; index++)
            {
                var current = value[index];
                if (current == quote)
                {
                    if (index != value.Length - 1 || result.Length == 0)
                        throw new FormatException("Expected one non-empty font family.");
                    return result.ToString();
                }
                if (current == '\\')
                {
                    index++;
                    if (index >= value.Length || value[index] is not ('\\' or '\'' or '"'))
                        throw new FormatException("Unsupported font family escape.");
                    current = value[index];
                }
                if (current is '\r' or '\n')
                    throw new FormatException("A font family string cannot contain a line break.");
                result.Append(current);
            }
            throw new FormatException("Unterminated font family string.");
        }

        var words = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words)
        {
            foreach (var character in word)
            {
                if (!char.IsLetterOrDigit(character) && character is not ('_' or '-'))
                    throw new FormatException("Expected a single font family name.");
            }
        }
        return string.Join(" ", words);
    }

    internal static FontWeight ParseWeight(string value)
    {
        if (value.Equals("normal", StringComparison.OrdinalIgnoreCase))
            return FontWeight.Normal;
        if (value.Equals("bold", StringComparison.OrdinalIgnoreCase))
            return FontWeight.Bold;
        var weight = UiCssValueConverters.ParseInteger(value);
        if (weight is < 100 or > 900 || weight % 100 != 0)
            throw new FormatException("Font weight must be 100 through 900 in steps of 100.");
        return (FontWeight)weight;
    }

    internal static FontStyle ParseStyle(string value) => value.ToLowerInvariant() switch
    {
        "normal" => FontStyle.Normal,
        "italic" => FontStyle.Italic,
        "oblique" => FontStyle.Oblique,
        _ => throw new FormatException("Font style must be normal, italic or oblique.")
    };

    internal static double ParseLineHeight(string value) =>
        value.Equals("normal", StringComparison.OrdinalIgnoreCase)
            ? 1d
            : UiCssValueConverters.ParseNumber(value);

    internal static IReadOnlyList<UiStyleSetter> ParseFont(string value)
    {
        FontStyle? style = null;
        FontWeight? weight = null;
        var normals = 0;
        var position = 0;
        double size;
        while (true)
        {
            var token = ReadToken(value, ref position);
            if (token.EndsWith("px", StringComparison.OrdinalIgnoreCase))
            {
                size = UiCssValueConverters.ParseLength(token);
                break;
            }
            if (token.Equals("normal", StringComparison.OrdinalIgnoreCase))
            {
                normals++;
                if (normals > 2)
                    throw new FormatException("Too many normal font components.");
            }
            else if (token.Equals("italic", StringComparison.OrdinalIgnoreCase) ||
                     token.Equals("oblique", StringComparison.OrdinalIgnoreCase))
            {
                if (style.HasValue)
                    throw new FormatException("Font style is repeated.");
                style = ParseStyle(token);
            }
            else
            {
                if (weight.HasValue)
                    throw new FormatException("Font weight is repeated.");
                weight = ParseWeight(token);
            }
        }

        if (normals + (style.HasValue ? 1 : 0) + (weight.HasValue ? 1 : 0) > 2)
            throw new FormatException("Too many font style or weight components.");
        SkipWhitespace(value, ref position);
        var lineHeight = 1d;
        if (position < value.Length && value[position] == '/')
        {
            position++;
            lineHeight = ParseLineHeight(ReadToken(value, ref position));
        }
        SkipWhitespace(value, ref position);
        var family = ParseFamily(value[position..]);
        return
        [
            UiStyleSetter.Create(UiNode.FontFamilyProperty, family),
            UiStyleSetter.Create(UiNode.FontWeightProperty, weight ?? FontWeight.Normal),
            UiStyleSetter.Create(UiNode.FontStyleProperty, style ?? FontStyle.Normal),
            UiStyleSetter.Create(UiNode.FontSizeProperty, size),
            UiStyleSetter.Create(UiNode.LineHeightProperty, lineHeight)
        ];
    }

    private static string ReadToken(string value, ref int position)
    {
        SkipWhitespace(value, ref position);
        var start = position;
        while (position < value.Length && !char.IsWhiteSpace(value[position]) && value[position] != '/')
            position++;
        if (start == position)
            throw new FormatException("A font component is missing.");
        return value[start..position];
    }

    private static void SkipWhitespace(string value, ref int position)
    {
        while (position < value.Length && char.IsWhiteSpace(value[position]))
            position++;
    }
}

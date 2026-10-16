namespace PanguEngine.Client.UI.Drawing;

public readonly partial record struct Color
{
    /// <summary>Parses an RGB or RGBA hexadecimal color with an optional leading #.</summary>
    /// <param name="value">Three, four, six, or eight ASCII hexadecimal digits, optionally preceded by #, without whitespace.</param>
    /// <param name="color">The parsed non-premultiplied color, or the default color when the format is invalid.</param>
    /// <returns>True when the format is valid; otherwise, false.</returns>
    /// <remarks>Short forms repeat each digit. Alpha is last and defaults to fully opaque when omitted.</remarks>
    public static bool TryFromHex(string value, out Color color)
    {
        color = default;
        var hex = value.AsSpan();
        if (!hex.IsEmpty && hex[0] == '#')
            hex = hex[1..];
        if (hex.Length is not (3 or 4 or 6 or 8))
            return false;
        foreach (var digit in hex)
        {
            if (!char.IsAsciiHexDigit(digit))
                return false;
        }

        if (hex.Length is 3 or 4)
        {
            color = new Color(
                (byte)(HexValue(hex[0]) * 17),
                (byte)(HexValue(hex[1]) * 17),
                (byte)(HexValue(hex[2]) * 17),
                hex.Length == 4 ? (byte)(HexValue(hex[3]) * 17) : (byte)255);
        }
        else
        {
            color = new Color(
                ReadHexByte(hex, 0), ReadHexByte(hex, 2), ReadHexByte(hex, 4),
                hex.Length == 8 ? ReadHexByte(hex, 6) : (byte)255);
        }
        return true;
    }

    /// <summary>Gets the value of a hexadecimal digit.</summary>
    private static int HexValue(char digit) => digit switch
    {
        >= '0' and <= '9' => digit - '0',
        >= 'A' and <= 'F' => digit - 'A' + 10,
        _ => digit - 'a' + 10
    };

    /// <summary>Reads a hexadecimal byte at the supplied digit offset.</summary>
    private static byte ReadHexByte(ReadOnlySpan<char> hex, int offset) =>
        (byte)((HexValue(hex[offset]) << 4) | HexValue(hex[offset + 1]));
}

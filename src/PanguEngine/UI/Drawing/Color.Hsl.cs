namespace PanguEngine.UI.Drawing;

public readonly partial record struct Color
{
    /// <summary>Creates a non-premultiplied sRGB color from HSL and alpha coverage.</summary>
    /// <param name="hueDegrees">The finite hue in degrees, wrapped around a full turn.</param>
    /// <param name="saturationPercent">The finite saturation, clamped to the range from zero to one hundred.</param>
    /// <param name="lightnessPercent">The finite lightness, clamped to the range from zero to one hundred.</param>
    /// <param name="alpha">The finite alpha coverage, clamped to the range from zero to one.</param>
    /// <returns>The color with byte channels rounded to the nearest integer, with midpoint values rounded upward.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An argument is not finite.</exception>
    public static Color FromHsl(double hueDegrees, double saturationPercent, double lightnessPercent, double alpha = 1)
    {
        if (!double.IsFinite(hueDegrees))
            throw new ArgumentOutOfRangeException(nameof(hueDegrees), "The hue must be finite.");
        if (!double.IsFinite(saturationPercent))
            throw new ArgumentOutOfRangeException(nameof(saturationPercent), "The saturation must be finite.");
        if (!double.IsFinite(lightnessPercent))
            throw new ArgumentOutOfRangeException(nameof(lightnessPercent), "The lightness must be finite.");
        if (!double.IsFinite(alpha))
            throw new ArgumentOutOfRangeException(nameof(alpha), "The alpha coverage must be finite.");

        var hue = hueDegrees % 360;
        if (hue < 0)
            hue += 360;
        return FromNormalizedHsl(
            hue,
            Math.Clamp(saturationPercent, 0, 100),
            Math.Clamp(lightnessPercent, 0, 100),
            QuantizeChannel(Math.Clamp(alpha, 0, 1) * 255));
    }

    /// <summary>Creates an sRGB color from a normalized hue in degrees, bounded S/L percentages, and a byte alpha.</summary>
    internal static Color FromNormalizedHsl(double hue, double saturation, double lightness, byte alpha)
    {
        var chroma = 2 * Math.Min(lightness, 100 - lightness) * saturation / 100;
        var sector = hue % 120;
        var secondary = chroma * (sector <= 60 ? sector : 120 - sector) / 60;
        var offset = lightness - chroma / 2;
        var (red, green, blue) = hue switch
        {
            < 60 => (chroma, secondary, 0d),
            < 120 => (secondary, chroma, 0d),
            < 180 => (0d, chroma, secondary),
            < 240 => (0d, secondary, chroma),
            < 300 => (secondary, 0d, chroma),
            _ => (chroma, 0d, secondary)
        };
        return new Color(
            QuantizeChannel((red + offset) / 100 * 255),
            QuantizeChannel((green + offset) / 100 * 255),
            QuantizeChannel((blue + offset) / 100 * 255),
            alpha);
    }

    /// <summary>Rounds a finite byte channel to the nearest clamped integer, with midpoint values rounded upward.</summary>
    internal static byte QuantizeChannel(double channel) =>
        (byte)Math.Round(Math.Clamp(channel, 0, 255), MidpointRounding.AwayFromZero);
}

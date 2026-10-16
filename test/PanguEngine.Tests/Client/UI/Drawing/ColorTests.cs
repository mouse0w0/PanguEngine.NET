using PanguEngine.Client.UI.Drawing;

namespace PanguEngine.Tests.Client.UI.Drawing;

public sealed class ColorTests
{
    [Theory]
    [InlineData("#abc", 170, 187, 204, 255)]
    [InlineData("abc", 170, 187, 204, 255)]
    [InlineData("#AbC8", 170, 187, 204, 136)]
    [InlineData("AbC8", 170, 187, 204, 136)]
    [InlineData("#aBcDeF", 171, 205, 239, 255)]
    [InlineData("aBcDeF", 171, 205, 239, 255)]
    [InlineData("#12345678", 18, 52, 86, 120)]
    [InlineData("12345678", 18, 52, 86, 120)]
    [InlineData("#f000", 255, 0, 0, 0)]
    public void TryFromHexParsesChannelsWithAnOptionalPrefix(string text, byte r, byte g, byte b, byte a)
    {
        Assert.True(Color.TryFromHex(text, out var color));
        Assert.Equal(new Color(r, g, b, a), color);
    }

    [Theory]
    [InlineData("")]
    [InlineData("#")]
    [InlineData("#12")]
    [InlineData("#12345")]
    [InlineData("ggg")]
    [InlineData("ＦＦＦ")]
    [InlineData("##abc")]
    [InlineData("0xabcdef")]
    [InlineData(" abc")]
    [InlineData("abc ")]
    [InlineData("a bc")]
    [InlineData("abc;")]
    public void TryFromHexRejectsMalformedValuesAndClearsTheOutput(string text)
    {
        var color = Color.White;

        Assert.False(Color.TryFromHex(text, out color));
        Assert.Equal(default, color);
    }

    [Theory]
    [InlineData(0, 100, 50, 1, 255, 0, 0, 255)]
    [InlineData(60, 100, 50, .5, 255, 255, 0, 128)]
    [InlineData(120, 100, 50, 1, 0, 255, 0, 255)]
    [InlineData(180, 100, 50, .5, 0, 255, 255, 128)]
    [InlineData(240, 100, 50, 1, 0, 0, 255, 255)]
    [InlineData(300, 100, 50, .5, 255, 0, 255, 128)]
    [InlineData(90, 100, 50, 1, 128, 255, 0, 255)]
    [InlineData(150, 100, 50, 1, 0, 255, 128, 255)]
    [InlineData(210, 100, 50, 1, 0, 128, 255, 255)]
    [InlineData(270, 100, 50, 1, 128, 0, 255, 255)]
    [InlineData(330, 100, 50, 1, 255, 0, 128, 255)]
    [InlineData(-60, 100, 50, 1, 255, 0, 255, 255)]
    [InlineData(390, 100, 50, .5, 255, 128, 0, 128)]
    [InlineData(720, 100, 50, 1, 255, 0, 0, 255)]
    [InlineData(double.MaxValue, 0, 50, 1, 128, 128, 128, 255)]
    [InlineData(30, 120, -10, 1.5, 0, 0, 0, 255)]
    [InlineData(30, -20, 120, -.5, 255, 255, 255, 0)]
    [InlineData(0, 50, 50, 1, 191, 64, 64, 255)]
    [InlineData(0, 100, 5, 1, 26, 0, 0, 255)]
    [InlineData(6, 100, 50, 1, 255, 26, 0, 255)]
    [InlineData(0, 100, 95, 1, 255, 230, 230, 255)]
    [InlineData(66, 100, 50, 1, 230, 255, 0, 255)]
    [InlineData(186, 100, 50, 1, 0, 230, 255, 255)]
    public void FromHslUsesDegreesPercentagesAndClampedCoverage(
        double hue, double saturation, double lightness, double alpha, byte r, byte g, byte b, byte a)
    {
        Assert.Equal(new Color(r, g, b, a), Color.FromHsl(hue, saturation, lightness, alpha));
    }

    [Fact]
    public void FromHslDefaultsToOpaque()
    {
        Assert.Equal(new Color(255, 0, 0), Color.FromHsl(0, 100, 50));
    }

    [Theory]
    [InlineData(double.NaN, 100, 50, 1, "hueDegrees")]
    [InlineData(0, double.PositiveInfinity, 50, 1, "saturationPercent")]
    [InlineData(0, 100, double.NegativeInfinity, 1, "lightnessPercent")]
    [InlineData(0, 100, 50, double.NaN, "alpha")]
    public void FromHslRejectsNonFiniteArguments(
        double hue, double saturation, double lightness, double alpha, string parameter)
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => Color.FromHsl(hue, saturation, lightness, alpha));

        Assert.Equal(parameter, error.ParamName);
    }

    [Theory]
    [InlineData("WhItE", 255, 255, 255, 255)]
    [InlineData("black", 0, 0, 0, 255)]
    [InlineData("green", 0, 128, 0, 255)]
    [InlineData("lime", 0, 255, 0, 255)]
    [InlineData("aliceblue", 240, 248, 255, 255)]
    [InlineData("CornflowerBlue", 100, 149, 237, 255)]
    [InlineData("RebeccaPurple", 102, 51, 153, 255)]
    [InlineData("TRANSPARENT", 0, 0, 0, 0)]
    public void FindsStandardNames(string name, byte r, byte g, byte b, byte a)
    {
        Assert.True(Color.TryFromName(name, out var color));
        Assert.Equal(new Color(r, g, b, a), color);
    }

    [Fact]
    public void NamedPropertiesExposeStandardColorsAndAliases()
    {
        Assert.Equal(new Color(255, 255, 255), Color.White);
        Assert.Equal(new Color(0, 0, 0), Color.Black);
        Assert.Equal(new Color(0, 128, 0), Color.Green);
        Assert.Equal(new Color(0, 255, 0), Color.Lime);
        Assert.Equal(new Color(100, 149, 237), Color.CornflowerBlue);
        Assert.Equal(new Color(102, 51, 153), Color.RebeccaPurple);
        Assert.Equal(new Color(0, 0, 0, 0), Color.Transparent);
        Assert.Equal(Color.Aqua, Color.Cyan);
        Assert.Equal(Color.Fuchsia, Color.Magenta);
        Assert.Equal(Color.Gray, Color.Grey);
        Assert.Equal(Color.DarkGray, Color.DarkGrey);
        Assert.Equal(Color.DarkSlateGray, Color.DarkSlateGrey);
        Assert.Equal(Color.DimGray, Color.DimGrey);
        Assert.Equal(Color.LightGray, Color.LightGrey);
        Assert.Equal(Color.LightSlateGray, Color.LightSlateGrey);
        Assert.Equal(Color.SlateGray, Color.SlateGrey);
    }

    [Theory]
    [InlineData("aqua", "cyan")]
    [InlineData("fuchsia", "magenta")]
    [InlineData("gray", "grey")]
    [InlineData("darkgray", "darkgrey")]
    [InlineData("darkslategray", "darkslategrey")]
    [InlineData("dimgray", "dimgrey")]
    [InlineData("lightgray", "lightgrey")]
    [InlineData("lightslategray", "lightslategrey")]
    [InlineData("slategray", "slategrey")]
    public void FindsAliases(string name, string alias)
    {
        Assert.True(Color.TryFromName(name, out var color));
        Assert.True(Color.TryFromName(alias, out var alternative));
        Assert.Equal(color, alternative);
    }

    [Theory]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData(" white")]
    [InlineData("white ")]
    [InlineData("\"white\"")]
    [InlineData("white blue")]
    [InlineData("white()")]
    [InlineData("#fff")]
    [InlineData("rgb(255 255 255)")]
    [InlineData("currentColor")]
    [InlineData("CanvasText")]
    public void RejectsValuesThatAreNotCompleteStandardNames(string name)
    {
        Assert.False(Color.TryFromName(name, out var color));
        Assert.Equal(default, color);
    }
}

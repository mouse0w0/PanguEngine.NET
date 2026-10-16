using System.Globalization;
using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Tests.Client.UI.Styling;

public sealed class UiCssColorTests
{
    [Theory]
    [InlineData("#AbC8", 170, 187, 204, 136)]
    [InlineData("rgb(255, 128, 0)", 255, 128, 0, 255)]
    [InlineData("rgb(100%, 50%, 0%, 50%)", 255, 128, 0, 128)]
    [InlineData("rgba(255, 128, 0)", 255, 128, 0, 255)]
    [InlineData("rgba(255, 128, 0, .5)", 255, 128, 0, 128)]
    [InlineData("RGB( +255 50% 0 / 5E-1 )", 255, 128, 0, 128)]
    [InlineData("rgba(2.55e2 127.5 0/50%)", 255, 128, 0, 128)]
    [InlineData("rgba(255 128 0)", 255, 128, 0, 255)]
    [InlineData("rgb(1\t2\n3\r/\f.5)", 1, 2, 3, 128)]
    [InlineData("rgb(300 -10 0 / 1.5)", 255, 0, 0, 255)]
    [InlineData("rgb(120% -20% 0% / -20%)", 255, 0, 0, 0)]
    [InlineData("rgb(1e308 0 0 / 1e308%)", 255, 0, 0, 255)]
    [InlineData("rgb(.49 .5 .51 / .5)", 0, 1, 1, 128)]
    public void ParsesHexAndRgbChannels(string text, byte r, byte g, byte b, byte a)
    {
        Assert.Equal(new Color(r, g, b, a), UiCssValueConverters.ParseColor(text));
    }

    [Theory]
    [InlineData("hsl(0 100% 50%)", 255, 0, 0, 255)]
    [InlineData("hsl(60, 100%, 50%, 50%)", 255, 255, 0, 128)]
    [InlineData("hsla(120, 100%, 50%)", 0, 255, 0, 255)]
    [InlineData("hsla(180, 100%, 50%, .5)", 0, 255, 255, 128)]
    [InlineData("hsla(300 100% 50% / 50%)", 255, 0, 255, 128)]
    [InlineData("hsla(180 100% 50%)", 0, 255, 255, 255)]
    [InlineData("HSL(390DEG 100% 50% / 50%)", 255, 128, 0, 128)]
    [InlineData("hsl(186deg 100% 50%)", 0, 230, 255, 255)]
    [InlineData("hsl(200grad 100% 50%)", 0, 255, 255, 255)]
    [InlineData("hsl(3.141592653589793rad 100% 50%)", 0, 255, 255, 255)]
    [InlineData("hsl(.5turn 100% 50%)", 0, 255, 255, 255)]
    [InlineData("hsl(1e308turn 100% 50%)", 255, 0, 0, 255)]
    [InlineData("hsl(-1e308grad 0% 50%)", 128, 128, 128, 255)]
    [InlineData("hsl(30 120% -10%)", 0, 0, 0, 255)]
    [InlineData("hsl(30 -20% 120% / 120%)", 255, 255, 255, 255)]
    public void ParsesHslSyntaxAndUnits(string text, byte r, byte g, byte b, byte a)
    {
        Assert.Equal(new Color(r, g, b, a), UiCssValueConverters.ParseColor(text));
    }

    [Fact]
    public void NamedVariablesProduceColorsAndSolidBrushes()
    {
        var panel = new Panel();
        Apply(panel, """
            Panel {
                --accent: CornflowerBlue;
                color: ReBeCcApUrPlE;
                background: var(--accent);
                border-color: darkslategrey;
            }
            """);

        Assert.Equal(Color.RebeccaPurple, panel.Color);
        Assert.Equal(new SolidColorBrush(Color.CornflowerBlue), Assert.IsType<SolidColorBrush>(panel.Background));
        Assert.Equal(new SolidColorBrush(Color.DarkSlateGray), Assert.IsType<SolidColorBrush>(panel.BorderBrush));
    }

    [Theory]
    [InlineData("")]
    [InlineData("#ggg")]
    [InlineData("abc")]
    [InlineData("rgb (1 2 3)")]
    [InlineData("rgb()")]
    [InlineData("rgb(1 2)")]
    [InlineData("rgb(1, 2 3)")]
    [InlineData("rgb(1 2, 3)")]
    [InlineData("rgb(1,,2,3)")]
    [InlineData("rgb(1,2,3,)")]
    [InlineData("rgb(1,2,3/.5)")]
    [InlineData("rgb(1 2 3 .5)")]
    [InlineData("rgb(1 2 3/)")]
    [InlineData("rgb(1 2 3/.5/.5)")]
    [InlineData("rgb(1,2,3,.5,0)")]
    [InlineData("rgb(1,20%,3)")]
    [InlineData("rgb(1 2 3)extra")]
    [InlineData("rgb(1 2 3))")]
    [InlineData("rgb(1 2 3")]
    [InlineData("rgb(1. 2 3)")]
    [InlineData("rgb(. 2 3)")]
    [InlineData("rgb(+ 2 3)")]
    [InlineData("rgb(1e 2 3)")]
    [InlineData("rgb(1e+ 2 3)")]
    [InlineData("rgb(1.2.3 2 3)")]
    [InlineData("rgb(0x10 2 3)")]
    [InlineData("rgb(1e999 0 0)")]
    [InlineData("rgb(NaN 0 0)")]
    [InlineData("rgb(Infinity 0 0)")]
    [InlineData("rgb(1px 2 3)")]
    [InlineData("rgb(1 % 2 3)")]
    [InlineData("rgb(1 2 3/.5deg)")]
    [InlineData("rgb(1\v2 3)")]
    [InlineData("rgb(1\u00a02 3)")]
    [InlineData("hsl(30 100 50)")]
    [InlineData("hsl(30% 100% 50%)")]
    [InlineData("hsl(30px 100% 50%)")]
    [InlineData("hsl(30 deg 100% 50%)")]
    [InlineData("hsl(30 100% 50% extra)")]
    [InlineData("rgb(none 0 0)")]
    [InlineData("rgb(calc(100 + 20) 0 0)")]
    [InlineData("hsl(from #fff h s l)")]
    [InlineData("currentColor")]
    [InlineData("unknown")]
    [InlineData("white blue")]
    [InlineData("\"white\"")]
    [InlineData("white()")]
    [InlineData("CanvasText")]
    public void RejectsMalformedOrUnsupportedColors(string text)
    {
        Assert.Throws<FormatException>(() => UiCssValueConverters.ParseColor(text));
    }

    [Fact]
    public void ColorNumbersUseInvariantCultureWithoutChangingOtherConverters()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

            Assert.Equal(new Color(128, 0, 0, 128), UiCssValueConverters.ParseColor("rgb(1.275e2 0 0 / .5)"));
            Assert.Throws<FormatException>(() => UiCssValueConverters.ParseNumber("1e2"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public void VariablesAndCommentsProduceColorsAndSolidBrushes()
    {
        var panel = new Panel();
        Apply(panel, """
            Panel {
                --accent: #3698;
                --hue: 30deg;
                --channels: 255 50% 0;
                --alpha: 50%;
                color: var(--accent);
                background: hsl(var(--hue) /* channel gap */ 100% 50% / var(--alpha));
                border-color: rgb(var(--channels) / var(--alpha));
            }
            """);

        Assert.Equal(new Color(51, 102, 153, 136), panel.Color);
        var expected = new SolidColorBrush(255, 128, 0, 128);
        Assert.Equal(expected, Assert.IsType<SolidColorBrush>(panel.Background));
        Assert.Equal(expected, Assert.IsType<SolidColorBrush>(panel.BorderBrush));
    }

    [Theory]
    [InlineData("rgb(1, 2 3)")]
    [InlineData("var(--missing, rgb(1, 2 3))")]
    [InlineData("hsl(var(--hue)deg 100% 50%)")]
    [InlineData("rgb(0 0 0 / var(--alpha)%)")]
    [InlineData("rgb(1/**/2 3 4)")]
    [InlineData("hsl(30/**/deg 100% 50%)")]
    [InlineData("rgb/**/(1 2 3)")]
    [InlineData("unknown")]
    [InlineData("var(--missing, unknown)")]
    [InlineData("var(--missing, abc)")]
    [InlineData("#ggg")]
    public void InvalidConsumedColorsKeepDeclarationDiagnostics(string value)
    {
        var panel = new Panel();
        var css = "Panel {\n    --hue: 30; --alpha: 50;\n    color: " + value + ";\n}";

        var error = Assert.Throws<UiStyleParseException>(() => Apply(panel, css));

        Assert.Equal(UiStyleParseError.InvalidValue, error.Error);
        Assert.Equal("colors.css", error.SourceName);
        Assert.Equal(3, error.Line);
        Assert.Equal(12, error.Column);
        Assert.IsType<FormatException>(error.InnerException);
    }

    private static void Apply(UiNode root, string css)
    {
        var screen = new UiScreen(root);
        screen.SetBaseStyleSheets([]);
        screen.SetStyleSheets([UiStyleSheet.Parse(css, "colors.css")]);
        root.UpdateStyles();
    }
}

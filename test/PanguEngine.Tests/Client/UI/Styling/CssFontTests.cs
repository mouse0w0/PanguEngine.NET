using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Styling;
using PanguEngine.Graphics.Text;

namespace PanguEngine.Tests.Client.UI.Styling;

public sealed class CssFontTests
{
    [Theory]
    [InlineData("italic bold 16px/1.5 \"Family Name\"")]
    [InlineData("bold italic 16px / 1.5 Family Name")]
    [InlineData("italic 700 16px/1.5 'Family Name'")]
    public void ShorthandSetsAllFiveProperties(string value)
    {
        var root = new Panel();
        Apply(root, $"Panel {{ font: {value}; }}");

        Assert.Equal("Family Name", root.FontFamily);
        Assert.Equal(FontWeight.Bold, root.FontWeight);
        Assert.Equal(FontStyle.Italic, root.FontStyle);
        Assert.Equal(16d, root.FontSize);
        Assert.Equal(1.5, root.LineHeight);
    }

    [Fact]
    public void ShorthandResetsOmittedValuesAndLeavesInheritedColor()
    {
        var root = new Panel { StyleId = "root" };
        var child = new Text();
        root.Children.Add(child);
        Apply(root, """
            #root { font-weight: bold; font-style: italic; line-height: 1.8; color: #123456; }
            Text { font: 16px "Family"; font-weight: 600; }
            """);

        Assert.Equal(FontWeight.SemiBold, child.FontWeight);
        Assert.Equal(FontStyle.Normal, child.FontStyle);
        Assert.Equal(1d, child.LineHeight);
        Assert.Equal(new Color(0x12, 0x34, 0x56), child.Color);
    }

    [Fact]
    public void FontVariablesImportanceAndLocalMaskingWorkPerProperty()
    {
        var root = new Panel { FontSize = 22 };
        Apply(root, """
            Panel {
                --font: italic 700 18px/1.5 "Family";
                font: var(--font) !important;
                font-weight: 100;
                font-style: normal !important;
            }
            """);

        Assert.Equal(22d, root.FontSize);
        Assert.Equal(FontWeight.Bold, root.FontWeight);
        Assert.Equal(FontStyle.Normal, root.FontStyle);
        Assert.Equal("font", Assert.Single(root.GetStyleValueSources(Node.FontFamilyProperty)).CssPropertyName);
        Assert.True(Assert.Single(root.GetStyleValueSources(Node.FontSizeProperty)).IsMaskedByLocalValue);
        root.ClearValue(Node.FontSizeProperty);
        Assert.Equal(18d, root.FontSize);
    }

    [Fact]
    public void ChildOverridesClearAndReparentUsesCurrentInheritedValues()
    {
        var first = new Panel { FontFamily = "First", FontWeight = FontWeight.Bold, LineHeight = 1.5 };
        var second = new Panel { FontFamily = "Second", FontWeight = FontWeight.Light, LineHeight = 2 };
        var child = new Text { FontStyle = FontStyle.Italic };
        first.Children.Add(child);

        Assert.Equal("First", child.FontFamily);
        Assert.Equal(FontWeight.Bold, child.FontWeight);
        child.FontWeight = FontWeight.Normal;
        first.FontWeight = FontWeight.Black;
        Assert.Equal(FontWeight.Normal, child.FontWeight);
        child.ClearValue(Node.FontWeightProperty);
        Assert.Equal(FontWeight.Black, child.FontWeight);

        first.Children.Remove(child);
        second.Children.Add(child);

        Assert.Equal("Second", child.FontFamily);
        Assert.Equal(FontWeight.Light, child.FontWeight);
        Assert.Equal(2d, child.LineHeight);
        Assert.Equal(FontStyle.Italic, child.FontStyle);
    }

    [Fact]
    public void BindingMasksInheritedFamilyUntilCleared()
    {
        var root = new Panel { FontFamily = "Parent" };
        var source = new Panel { FontFamily = "Bound" };
        var child = new Text();
        root.Children.Add(child);
        child.Bind(Node.FontFamilyProperty, source, Node.FontFamilyProperty);

        root.FontFamily = "Changed parent";
        Assert.Equal("Bound", child.FontFamily);
        source.FontFamily = "Changed source";
        Assert.Equal("Changed source", child.FontFamily);
        child.ClearValue(Node.FontFamilyProperty);
        Assert.Equal("Changed parent", child.FontFamily);
    }

    [Theory]
    [InlineData("\"Family, Name\"", "Family, Name")]
    [InlineData("Family   Name", "Family Name")]
    [InlineData("'Family Name'", "Family Name")]
    [InlineData("\"Family\\\\Name\"", "Family\\Name")]
    [InlineData("\"Family\\\"Name\"", "Family\"Name")]
    public void FamilyNamesPreserveQuotedContentAndNormalizeWords(string value, string expected)
    {
        var root = new Panel();
        Apply(root, $"Panel {{ font-family: {value}; }}");

        Assert.Equal(expected, root.FontFamily);
    }

    [Theory]
    [InlineData("normal italic 16px/normal Family", FontStyle.Italic, FontWeight.Normal)]
    [InlineData("normal bold 16px Family", FontStyle.Normal, FontWeight.Bold)]
    [InlineData("normal normal 16px Family", FontStyle.Normal, FontWeight.Normal)]
    [InlineData("16px Family", FontStyle.Normal, FontWeight.Normal)]
    [InlineData("oblique 16px Family", FontStyle.Oblique, FontWeight.Normal)]
    public void NormalComponentsFillOnlyAvailableSlots(string value, FontStyle style, FontWeight weight)
    {
        var root = new Panel();
        Apply(root, $"Panel {{ font: {value}; }}");

        Assert.Equal(style, root.FontStyle);
        Assert.Equal(weight, root.FontWeight);
        Assert.Equal(1d, root.LineHeight);
    }

    [Theory]
    [InlineData("normal", FontWeight.Normal)]
    [InlineData("bold", FontWeight.Bold)]
    [InlineData("100", FontWeight.Thin)]
    [InlineData("200", FontWeight.ExtraLight)]
    [InlineData("300", FontWeight.Light)]
    [InlineData("400", FontWeight.Normal)]
    [InlineData("500", FontWeight.Medium)]
    [InlineData("600", FontWeight.SemiBold)]
    [InlineData("700", FontWeight.Bold)]
    [InlineData("800", FontWeight.ExtraBold)]
    [InlineData("900", FontWeight.Black)]
    public void AbsoluteWeightsMapToExistingFontWeights(string value, FontWeight expected)
    {
        var root = new Panel();
        Apply(root, $"Panel {{ font-weight: {value}; }}");

        Assert.Equal(expected, root.FontWeight);
    }

    [Theory]
    [InlineData("font-weight: 550;")]
    [InlineData("font-weight: bolder;")]
    [InlineData("font-style: oblique 15deg;")]
    [InlineData("font-family: A, B;")]
    [InlineData("font-family: \"\";")]
    [InlineData("line-height: 20px;")]
    [InlineData("line-height: 0;")]
    [InlineData("font: italic bold 16px;")]
    [InlineData("font: italic italic 16px Family;")]
    [InlineData("font: bold 700 16px Family;")]
    [InlineData("font: normal normal bold 16px Family;")]
    [InlineData("font: 16px/ Family;")]
    [InlineData("font: 16px/1.5;")]
    [InlineData("font: 16 Family;")]
    public void UnsupportedOrIncompleteValuesReportCssErrors(string declaration)
    {
        var root = new Panel();

        var error = Assert.Throws<StyleParseException>(() => Apply(root, $"Panel {{ {declaration} }}"));

        Assert.Equal(StyleParseError.InvalidValue, error.Error);
        Assert.Equal("fonts.css", error.SourceName);
        Assert.True(error.Line > 0);
        Assert.True(error.Column > 0);
    }

    private static Screen Apply(Node root, string css)
    {
        var screen = new Screen(root);
        screen.SetBaseStyleSheets([]);
        screen.SetStyleSheets([StyleSheet.Parse(css, "fonts.css")]);
        root.UpdateStyles();
        return screen;
    }
}

using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Drawing;
using PanguEngine.UI.Styling;

namespace PanguEngine.Tests.UI.Styling;

public sealed class CssKeywordTests
{
    [Theory]
    [InlineData("auto")]
    [InlineData("AuTo")]
    public void AutoDimensionsOverrideFixedStylesAndUseMeasuredContent(string keyword)
    {
        var panel = new Panel
        {
            StyleId = "target",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        panel.Children.Add(new Panel { Width = 120, Height = 40 });
        var screen = new Screen(panel);
        screen.SetBaseStyleSheets([StyleSheet.Parse("#target { width: 32px; height: 16px; }")]);
        screen.SetStyleSheets([StyleSheet.Parse($"#target {{ width: {keyword}; height: {keyword}; }}")]);
        panel.UpdateStyles();

        panel.Measure(new Size(500, 500));
        panel.Arrange(new Rect(0, 0, 500, 500));

        Assert.True(double.IsNaN(panel.Width));
        Assert.True(double.IsNaN(panel.Height));
        Assert.Equal(new Size(120, 40), panel.DesiredSize);
        Assert.Equal(new Rect(0, 0, 120, 40), panel.LayoutBounds);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("NoNe")]
    public void MaximumDimensionsNoneOverrideFiniteLimitsAndAllowStretch(string keyword)
    {
        var panel = new Panel();
        var screen = new Screen(panel);
        screen.SetBaseStyleSheets([StyleSheet.Parse("Panel { max-width: 80px; max-height: 40px; }")]);
        screen.SetStyleSheets([StyleSheet.Parse($"Panel {{ max-width: {keyword}; max-height: {keyword}; }}")]);
        panel.UpdateStyles();

        panel.Measure(new Size(500, 500));
        panel.Arrange(new Rect(0, 0, 200, 100));

        Assert.Equal(double.PositiveInfinity, panel.MaxWidth);
        Assert.Equal(double.PositiveInfinity, panel.MaxHeight);
        Assert.Equal(new Rect(0, 0, 200, 100), panel.LayoutBounds);
    }

    [Theory]
    [InlineData("background", "none")]
    [InlineData("background", "NoNe")]
    [InlineData("background-color", "none")]
    [InlineData("background-color", "NoNe")]
    [InlineData("border-color", "none")]
    [InlineData("border-color", "NoNe")]
    public void RegionBrushNoneClearsExistingStyleWithoutRemovingBorderThickness(string propertyName, string keyword)
    {
        var panel = new Panel();
        var property = propertyName == "border-color" ? Region.BorderBrushProperty : Region.BackgroundProperty;
        var screen = new Screen(panel);
        screen.SetBaseStyleSheets([
            StyleSheet.Parse($"Panel {{ {propertyName}: #123456; border-thickness: 4px; }}")
        ]);
        panel.UpdateStyles();
        Assert.Equal(new SolidColorBrush(0x12, 0x34, 0x56), panel.GetValue(property));

        screen.SetStyleSheets([StyleSheet.Parse($"Panel {{ {propertyName}: {keyword}; }}")]);
        panel.UpdateStyles();

        Assert.Null(panel.GetValue(property));
        Assert.Equal(new Thickness(4), panel.BorderThickness);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("NoNe")]
    public void ShapeBrushNoneClearsExistingFillAndStroke(string keyword)
    {
        var rectangle = new Rectangle();
        var screen = new Screen(rectangle);
        screen.SetBaseStyleSheets([StyleSheet.Parse("Rectangle { fill: #123456; stroke: #654321; }")]);
        rectangle.UpdateStyles();
        Assert.NotNull(rectangle.Fill);
        Assert.NotNull(rectangle.Stroke);

        screen.SetStyleSheets([StyleSheet.Parse($"Rectangle {{ fill: {keyword}; stroke: {keyword}; }}")]);
        rectangle.UpdateStyles();

        Assert.Null(rectangle.Fill);
        Assert.Null(rectangle.Stroke);
    }

    [Theory]
    [InlineData("transparent", 0, 0, 0, 0)]
    [InlineData("#123456", 0x12, 0x34, 0x56, 255)]
    [InlineData("#12345678", 0x12, 0x34, 0x56, 0x78)]
    public void BrushColorsStillProduceSolidBrushes(string value, byte r, byte g, byte b, byte a)
    {
        var panelResolver = new StyleResolver([], [
            StyleSheet.Parse($"Panel {{ background: {value}; border-color: {value}; }}")
        ]);
        var panelStyle = panelResolver.Resolve(new Panel());
        var shapeResolver = new StyleResolver([], [
            StyleSheet.Parse($"Rectangle {{ fill: {value}; stroke: {value}; }}")
        ]);
        var shapeStyle = shapeResolver.Resolve(new Rectangle());
        var expected = new SolidColorBrush(r, g, b, a);

        Assert.Equal(expected, Assert.IsType<SolidColorBrush>(panelStyle.GetValue(Region.BackgroundProperty)));
        Assert.Equal(expected, Assert.IsType<SolidColorBrush>(panelStyle.GetValue(Region.BorderBrushProperty)));
        Assert.Equal(expected, Assert.IsType<SolidColorBrush>(shapeStyle.GetValue(Shape.FillProperty)));
        Assert.Equal(expected, Assert.IsType<SolidColorBrush>(shapeStyle.GetValue(Shape.StrokeProperty)));
    }

    [Theory]
    [InlineData("120")]
    [InlineData("120px")]
    public void NumericLengthsRemainSupportedForAllDimensions(string value)
    {
        var resolver = new StyleResolver([], [
            StyleSheet.Parse($$"""
                               Panel { width: {{value}}; height: {{value}}; min-width: {{value}}; min-height: {{value}};
                                       max-width: {{value}}; max-height: {{value}}; }
                               """)
        ]);
        var style = resolver.Resolve(new Panel());

        Assert.Equal(120d, style.GetValue(UIElement.WidthProperty));
        Assert.Equal(120d, style.GetValue(UIElement.HeightProperty));
        Assert.Equal(120d, style.GetValue(UIElement.MinWidthProperty));
        Assert.Equal(120d, style.GetValue(UIElement.MinHeightProperty));
        Assert.Equal(120d, style.GetValue(UIElement.MaxWidthProperty));
        Assert.Equal(120d, style.GetValue(UIElement.MaxHeightProperty));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("0px")]
    public void ZeroMinimumDimensionsRemoveExistingLowerLimits(string value)
    {
        var panel = new Panel();
        var screen = new Screen(panel);
        screen.SetBaseStyleSheets([
            StyleSheet.Parse(
                "Panel { width: 20px; height: 10px; min-width: 100px; min-height: 50px; }")
        ]);
        panel.UpdateStyles();
        panel.Measure(new Size(500, 500));
        panel.Arrange(new Rect(0, 0, 500, 500));
        Assert.Equal(new Size(100, 50), panel.DesiredSize);

        screen.SetStyleSheets([StyleSheet.Parse($"Panel {{ min-width: {value}; min-height: {value}; }}")]);
        panel.UpdateStyles();
        panel.Measure(new Size(500, 500));
        panel.Arrange(new Rect(0, 0, 500, 500));

        Assert.Equal(0d, panel.MinWidth);
        Assert.Equal(0d, panel.MinHeight);
        Assert.Equal(new Size(20, 10), panel.DesiredSize);
        Assert.Equal(20d, panel.LayoutBounds.Width);
        Assert.Equal(10d, panel.LayoutBounds.Height);
    }

    [Theory]
    [InlineData("min-width", "auto")]
    [InlineData("min-width", "none")]
    [InlineData("min-height", "auto")]
    [InlineData("min-height", "none")]
    [InlineData("max-width", "auto")]
    [InlineData("max-height", "auto")]
    [InlineData("width", "none")]
    [InlineData("height", "none")]
    [InlineData("margin", "auto")]
    [InlineData("padding", "none")]
    [InlineData("border-thickness", "none")]
    [InlineData("width", "autopx")]
    [InlineData("height", "autopx")]
    [InlineData("max-width", "nonepx")]
    [InlineData("max-height", "nonepx")]
    [InlineData("width", "12em")]
    [InlineData("max-width", "12em")]
    [InlineData("background", "auto")]
    [InlineData("border-color", "nonepx")]
    public void KeywordsAreRejectedOutsideTheirPropertySemantics(string propertyName, string value)
    {
        var resolver = new StyleResolver([], [StyleSheet.Parse($"Panel {{ {propertyName}: {value}; }}")]);

        var error = Assert.Throws<StyleParseException>(() => resolver.Resolve(new Panel()));

        Assert.Equal(StyleParseError.InvalidValue, error.Error);
        Assert.IsType<FormatException>(error.InnerException);
    }

    [Theory]
    [InlineData("width")]
    [InlineData("height")]
    [InlineData("max-width")]
    [InlineData("max-height")]
    public void DimensionConvertersPreserveNegativeValueValidation(string propertyName)
    {
        var panel = new Panel();
        var screen = new Screen(panel);
        screen.SetStyleSheets([StyleSheet.Parse($"Panel {{ {propertyName}: -1px; }}")]);

        var error = Assert.Throws<StyleParseException>(panel.UpdateStyles);

        Assert.Equal(StyleParseError.InvalidValue, error.Error);
        Assert.IsType<ArgumentException>(error.InnerException);
    }

    [Fact]
    public void VariablesApplyDimensionAndBrushKeywordsAfterSubstitution()
    {
        var panel = new Panel { StyleId = "target" };
        var rectangle = new Rectangle();
        panel.Children.Add(rectangle);
        var screen = new Screen(panel);
        screen.SetBaseStyleSheets([
            StyleSheet.Parse("""
                             #target { width: 80px; height: 40px; max-width: 100px; max-height: 50px;
                                       background: #123456; border-color: #654321; }
                             Rectangle { fill: #123456; stroke: #654321; }
                             """)
        ]);
        screen.SetStyleSheets([
            StyleSheet.Parse("""
                             #target { --size: AuTo; --empty: NoNe;
                                       width: var(--size); height: var(--size);
                                       max-width: var(--empty); max-height: var(--empty);
                                       background-color: var(--empty); border-color: var(--empty); }
                             Rectangle { fill: var(--empty); stroke: var(--empty); }
                             """)
        ]);

        panel.UpdateStyles();

        Assert.True(double.IsNaN(panel.Width));
        Assert.True(double.IsNaN(panel.Height));
        Assert.Equal(double.PositiveInfinity, panel.MaxWidth);
        Assert.Equal(double.PositiveInfinity, panel.MaxHeight);
        Assert.Null(panel.Background);
        Assert.Null(panel.BorderBrush);
        Assert.Null(rectangle.Fill);
        Assert.Null(rectangle.Stroke);
    }
}
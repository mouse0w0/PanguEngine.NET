using PanguEngine.ComponentModel;
using PanguEngine.UI;
using PanguEngine.UI.Styling;

namespace PanguEngine.Tests.UI;

public sealed class UIElementPropertyValidationTests
{
    [Theory]
    [InlineData(nameof(UIElement.Width))]
    [InlineData(nameof(UIElement.Height))]
    [InlineData(nameof(UIElement.MinWidth))]
    [InlineData(nameof(UIElement.MinHeight))]
    [InlineData(nameof(UIElement.MaxWidth))]
    [InlineData(nameof(UIElement.MaxHeight))]
    [InlineData(nameof(UIElement.Opacity))]
    public void InvalidValuesPreserveValueNotificationsAndValidLayout(string propertyName)
    {
        var property = GetProperty(propertyName);
        var node = new ValidationNode();
        node.SetValue(property, 0.5);
        node.Measure(new Size(10, 10));
        node.Arrange(new Rect(0, 0, 10, 10));
        var notifications = 0;
        using var subscription = node.Subscribe(property, (_, _) => notifications++);

        foreach (var value in GetInvalidValues(propertyName))
        {
            var error = Assert.Throws<ArgumentException>(() => node.SetValue(property, value));

            Assert.Equal("value", error.ParamName);
            Assert.Contains(propertyName, error.Message);
            Assert.Equal(0.5, node.GetValue(property));
            Assert.True(node.IsMeasureValid);
            Assert.True(node.IsArrangeValid);
            Assert.Equal(0, notifications);
        }
    }

    [Theory]
    [InlineData(nameof(UIElement.Width))]
    [InlineData(nameof(UIElement.Height))]
    [InlineData(nameof(UIElement.MinWidth))]
    [InlineData(nameof(UIElement.MinHeight))]
    [InlineData(nameof(UIElement.MaxWidth))]
    [InlineData(nameof(UIElement.MaxHeight))]
    [InlineData(nameof(UIElement.Opacity))]
    public void ValidBoundariesAndDefaultFallbackRemainAccepted(string propertyName)
    {
        var property = GetProperty(propertyName);
        var node = new ValidationNode();

        foreach (var value in GetValidValues(propertyName))
        {
            node.SetValue(property, value);
            Assert.Equal(value, node.GetValue(property));
        }

        node.ClearValue(property);
        Assert.Equal(property.DefaultValue, node.GetValue(property));
    }

    [Theory]
    [InlineData(nameof(UIElement.Width), "width", "-1px")]
    [InlineData(nameof(UIElement.Height), "height", "-1px")]
    [InlineData(nameof(UIElement.MinWidth), "min-width", "-1px")]
    [InlineData(nameof(UIElement.MinHeight), "min-height", "-1px")]
    [InlineData(nameof(UIElement.MaxWidth), "max-width", "-1px")]
    [InlineData(nameof(UIElement.MaxHeight), "max-height", "-1px")]
    [InlineData(nameof(UIElement.Opacity), "opacity", "2")]
    public void CssConversionUsesTheRegisteredPropertyConstraint(string propertyName, string cssName, string text)
    {
        var property = GetProperty(propertyName);
        var node = new ValidationNode();
        node.Classes.Add("validated");
        var screen = new Screen(node);
        screen.SetStyleSheets([CreateStyleSheet(property, 0.5)]);
        node.UpdateStyles();
        var sheet = StyleSheet.Parse($".validated {{ {cssName}: {text}; }}", "node.css");

        screen.SetStyleSheets([sheet]);
        var error = Assert.Throws<StyleParseException>(node.UpdateStyles);

        Assert.Contains(propertyName, error.Message);
        Assert.Equal("node.css", error.SourceName);
        Assert.Equal(1, error.Line);
        Assert.Equal(cssName.Length, error.Length);
        Assert.Equal("value", Assert.IsType<ArgumentException>(error.InnerException).ParamName);
        Assert.Equal(0.5, node.GetValue(property));
    }

    private static Property<double> GetProperty(string name) => name switch
    {
        nameof(UIElement.Width) => UIElement.WidthProperty,
        nameof(UIElement.Height) => UIElement.HeightProperty,
        nameof(UIElement.MinWidth) => UIElement.MinWidthProperty,
        nameof(UIElement.MinHeight) => UIElement.MinHeightProperty,
        nameof(UIElement.MaxWidth) => UIElement.MaxWidthProperty,
        nameof(UIElement.MaxHeight) => UIElement.MaxHeightProperty,
        nameof(UIElement.Opacity) => UIElement.OpacityProperty,
        _ => throw new ArgumentException("Unknown property.", nameof(name))
    };

    private static double[] GetInvalidValues(string name) => name switch
    {
        nameof(UIElement.Width) or nameof(UIElement.Height) =>
            [-1, double.NegativeInfinity, double.PositiveInfinity],
        nameof(UIElement.MinWidth) or nameof(UIElement.MinHeight) =>
            [-1, double.NaN, double.NegativeInfinity, double.PositiveInfinity],
        nameof(UIElement.MaxWidth) or nameof(UIElement.MaxHeight) =>
            [-1, double.NaN, double.NegativeInfinity],
        _ => [-1, 1.01, double.NaN, double.NegativeInfinity, double.PositiveInfinity]
    };

    private static double[] GetValidValues(string name) => name switch
    {
        nameof(UIElement.Width) or nameof(UIElement.Height) => [0, double.MaxValue, double.NaN],
        nameof(UIElement.MaxWidth) or nameof(UIElement.MaxHeight) => [0, double.MaxValue, double.PositiveInfinity],
        nameof(UIElement.Opacity) => [0, 0.5, 1],
        _ => [0, double.MaxValue]
    };

    private static StyleSheet CreateStyleSheet(Property<double> property, double value) => new([
        new StyleRule(StyleSelector.For<ValidationNode>(), [StyleSetter.Create(property, value)])
    ]);

    private sealed class ValidationNode : UIElement
    {
    }
}
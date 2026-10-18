using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Styling;
using PanguEngine.ComponentModel;

namespace PanguEngine.Tests.Client.UI;

public sealed class NodePropertyValidationTests
{
    [Theory]
    [InlineData(nameof(Node.Width))]
    [InlineData(nameof(Node.Height))]
    [InlineData(nameof(Node.MinWidth))]
    [InlineData(nameof(Node.MinHeight))]
    [InlineData(nameof(Node.MaxWidth))]
    [InlineData(nameof(Node.MaxHeight))]
    [InlineData(nameof(Node.Opacity))]
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
    [InlineData(nameof(Node.Width))]
    [InlineData(nameof(Node.Height))]
    [InlineData(nameof(Node.MinWidth))]
    [InlineData(nameof(Node.MinHeight))]
    [InlineData(nameof(Node.MaxWidth))]
    [InlineData(nameof(Node.MaxHeight))]
    [InlineData(nameof(Node.Opacity))]
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
    [InlineData(nameof(Node.Width), "width", "-1px")]
    [InlineData(nameof(Node.Height), "height", "-1px")]
    [InlineData(nameof(Node.MinWidth), "min-width", "-1px")]
    [InlineData(nameof(Node.MinHeight), "min-height", "-1px")]
    [InlineData(nameof(Node.MaxWidth), "max-width", "-1px")]
    [InlineData(nameof(Node.MaxHeight), "max-height", "-1px")]
    [InlineData(nameof(Node.Opacity), "opacity", "2")]
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
        nameof(Node.Width) => Node.WidthProperty,
        nameof(Node.Height) => Node.HeightProperty,
        nameof(Node.MinWidth) => Node.MinWidthProperty,
        nameof(Node.MinHeight) => Node.MinHeightProperty,
        nameof(Node.MaxWidth) => Node.MaxWidthProperty,
        nameof(Node.MaxHeight) => Node.MaxHeightProperty,
        nameof(Node.Opacity) => Node.OpacityProperty,
        _ => throw new ArgumentException("Unknown property.", nameof(name))
    };

    private static double[] GetInvalidValues(string name) => name switch
    {
        nameof(Node.Width) or nameof(Node.Height) =>
            [-1, double.NegativeInfinity, double.PositiveInfinity],
        nameof(Node.MinWidth) or nameof(Node.MinHeight) =>
            [-1, double.NaN, double.NegativeInfinity, double.PositiveInfinity],
        nameof(Node.MaxWidth) or nameof(Node.MaxHeight) =>
            [-1, double.NaN, double.NegativeInfinity],
        _ => [-1, 1.01, double.NaN, double.NegativeInfinity, double.PositiveInfinity]
    };

    private static double[] GetValidValues(string name) => name switch
    {
        nameof(Node.Width) or nameof(Node.Height) => [0, double.MaxValue, double.NaN],
        nameof(Node.MaxWidth) or nameof(Node.MaxHeight) => [0, double.MaxValue, double.PositiveInfinity],
        nameof(Node.Opacity) => [0, 0.5, 1],
        _ => [0, double.MaxValue]
    };

    private static StyleSheet CreateStyleSheet(Property<double> property, double value) => new([
        new StyleRule(StyleSelector.For<ValidationNode>(), [StyleSetter.Create(property, value)])
    ]);

    private sealed class ValidationNode : Node
    {
    }
}

using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Styling;
using PanguEngine.ComponentModel;

namespace PanguEngine.Tests.Client.UI;

public sealed class UiNodePropertyValidationTests
{
    [Theory]
    [InlineData(nameof(UiNode.Width))]
    [InlineData(nameof(UiNode.Height))]
    [InlineData(nameof(UiNode.MinWidth))]
    [InlineData(nameof(UiNode.MinHeight))]
    [InlineData(nameof(UiNode.MaxWidth))]
    [InlineData(nameof(UiNode.MaxHeight))]
    [InlineData(nameof(UiNode.Opacity))]
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
    [InlineData(nameof(UiNode.Width))]
    [InlineData(nameof(UiNode.Height))]
    [InlineData(nameof(UiNode.MinWidth))]
    [InlineData(nameof(UiNode.MinHeight))]
    [InlineData(nameof(UiNode.MaxWidth))]
    [InlineData(nameof(UiNode.MaxHeight))]
    [InlineData(nameof(UiNode.Opacity))]
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
    [InlineData(nameof(UiNode.Width), "width", "-1px")]
    [InlineData(nameof(UiNode.Height), "height", "-1px")]
    [InlineData(nameof(UiNode.MinWidth), "min-width", "-1px")]
    [InlineData(nameof(UiNode.MinHeight), "min-height", "-1px")]
    [InlineData(nameof(UiNode.MaxWidth), "max-width", "-1px")]
    [InlineData(nameof(UiNode.MaxHeight), "max-height", "-1px")]
    [InlineData(nameof(UiNode.Opacity), "opacity", "2")]
    public void CssConversionUsesTheRegisteredPropertyConstraint(string propertyName, string cssName, string text)
    {
        var property = GetProperty(propertyName);
        var node = new ValidationNode();
        node.Classes.Add("validated");
        var screen = new UiScreen(node);
        screen.SetStyleSheets([CreateStyleSheet(property, 0.5)]);
        node.UpdateStyles();
        var sheet = UiStyleSheet.Parse($".validated {{ {cssName}: {text}; }}", "node.css");

        screen.SetStyleSheets([sheet]);
        var error = Assert.Throws<UiStyleParseException>(node.UpdateStyles);

        Assert.Contains(propertyName, error.Message);
        Assert.Equal("node.css", error.SourceName);
        Assert.Equal(1, error.Line);
        Assert.Equal(cssName.Length, error.Length);
        Assert.Equal("value", Assert.IsType<ArgumentException>(error.InnerException).ParamName);
        Assert.Equal(0.5, node.GetValue(property));
    }

    private static Property<double> GetProperty(string name) => name switch
    {
        nameof(UiNode.Width) => UiNode.WidthProperty,
        nameof(UiNode.Height) => UiNode.HeightProperty,
        nameof(UiNode.MinWidth) => UiNode.MinWidthProperty,
        nameof(UiNode.MinHeight) => UiNode.MinHeightProperty,
        nameof(UiNode.MaxWidth) => UiNode.MaxWidthProperty,
        nameof(UiNode.MaxHeight) => UiNode.MaxHeightProperty,
        nameof(UiNode.Opacity) => UiNode.OpacityProperty,
        _ => throw new ArgumentException("Unknown property.", nameof(name))
    };

    private static double[] GetInvalidValues(string name) => name switch
    {
        nameof(UiNode.Width) or nameof(UiNode.Height) =>
            [-1, double.NegativeInfinity, double.PositiveInfinity],
        nameof(UiNode.MinWidth) or nameof(UiNode.MinHeight) =>
            [-1, double.NaN, double.NegativeInfinity, double.PositiveInfinity],
        nameof(UiNode.MaxWidth) or nameof(UiNode.MaxHeight) =>
            [-1, double.NaN, double.NegativeInfinity],
        _ => [-1, 1.01, double.NaN, double.NegativeInfinity, double.PositiveInfinity]
    };

    private static double[] GetValidValues(string name) => name switch
    {
        nameof(UiNode.Width) or nameof(UiNode.Height) => [0, double.MaxValue, double.NaN],
        nameof(UiNode.MaxWidth) or nameof(UiNode.MaxHeight) => [0, double.MaxValue, double.PositiveInfinity],
        nameof(UiNode.Opacity) => [0, 0.5, 1],
        _ => [0, double.MaxValue]
    };

    private static UiStyleSheet CreateStyleSheet(Property<double> property, double value) => new([
        new UiStyleRule(UiStyleSelector.For<ValidationNode>(), [UiStyleSetter.Create(property, value)])
    ]);

    private sealed class ValidationNode : UiNode
    {
    }
}

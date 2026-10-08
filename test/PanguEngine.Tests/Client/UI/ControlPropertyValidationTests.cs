using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.ComponentModel;
using Path = PanguEngine.Client.UI.Controls.Path;

namespace PanguEngine.Tests.Client.UI;

public sealed class ControlPropertyValidationTests
{
    [Fact]
    public void NumericConstraintsRejectInvalidValuesBeforeCommit()
    {
        foreach (var (create, property, valid, invalid) in NumericCases())
        {
            var node = create();
            node.SetValue(property, valid);
            foreach (var value in invalid)
            {
                Assert.Throws<ArgumentException>(() => node.SetValue(property, value));
                Assert.Equal(valid, node.GetValue(property));
            }
        }
    }

    [Fact]
    public void ExistingEnumConstraintsRejectUndefinedValuesAtThePropertyBoundary()
    {
        AssertRejected(new Panel(), UiNode.HorizontalAlignmentProperty, (HorizontalAlignment)123);
        AssertRejected(new Panel(), UiNode.VerticalAlignmentProperty, (VerticalAlignment)123);
        AssertRejected(new ImageView(), ImageView.StretchProperty, (ImageStretch)123);
        AssertRejected(new Path(), Path.StretchProperty, (PathStretch)123);
    }

    [Fact]
    public void TextInputsRejectNullBeforePublishingOrSynchronizingChildren()
    {
        AssertRejected(new Text(), Text.ContentProperty, null!);
        AssertRejected(new Text(), Text.FontProperty, null!);
        AssertRejected(new Button(), Button.TextProperty, null!);
        AssertRejected(new Button(), Button.FontProperty, null!);
        AssertRejected(new TextBox(), TextBox.TextProperty, null!);
        AssertRejected(new TextBox(), TextBox.PlaceholderProperty, null!);
        AssertRejected(new TextBox(), TextBox.FontProperty, null!);
    }

    private static void AssertRejected<T>(UiNode node, Property<T> property, T value)
    {
        var previous = node.GetValue(property);
        var notifications = 0;
        using var subscription = node.Subscribe(property, (_, _) => notifications++);
        Assert.Throws<ArgumentException>(() => node.SetValue(property, value));
        Assert.Equal(previous, node.GetValue(property));
        Assert.Equal(0, notifications);
    }

    private static IEnumerable<(Func<UiNode> Create, Property<double> Property, double Valid, double[] Invalid)> NumericCases()
    {
        double[] nonNegative = [-1, double.NaN, double.PositiveInfinity, double.NegativeInfinity];
        double[] positive = [0, -1, double.NaN, double.PositiveInfinity, double.NegativeInfinity];
        yield return (() => new Button(), Button.FontSizeProperty, 12, positive);
        yield return (() => new TabView(), TabView.MinTabWidthProperty, 0, nonNegative);
        yield return (() => new TabView(), TabView.MaxTabWidthProperty, 240, nonNegative);
        yield return (() => new TabView(), TabView.VerticalTabStripWidthProperty, 0, nonNegative);
        yield return (() => new Rectangle(), Shape.StrokeThicknessProperty, 0, nonNegative);
        yield return (() => new Rectangle(), Shape.StrokeMiterLimitProperty, 1, [0.5, double.NaN, double.PositiveInfinity, double.NegativeInfinity]);
        yield return (() => new Text(), Text.FontSizeProperty, 12, positive);
        yield return (() => new Text(), Text.LineHeightProperty, 1, positive);
        yield return (() => new TextBox(), TextBox.FontSizeProperty, 12, positive);
        yield return (() => new ScrollBar(), ScrollBar.LargeChangeProperty, 0, nonNegative);
        yield return (() => new ScrollBar(), ScrollBar.MinimumThumbLengthProperty, 0, nonNegative);
    }
}

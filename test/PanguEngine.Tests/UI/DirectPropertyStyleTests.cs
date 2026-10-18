using PanguEngine.ComponentModel;
using PanguEngine.UI;
using PanguEngine.UI.Drawing;
using PanguEngine.UI.Styling;

namespace PanguEngine.Tests.UI;

public sealed class DirectPropertyStyleTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void StyleRulesRejectDirectProperties(bool wildcard, bool readOnly)
    {
        var selector = wildcard
            ? Assert.Single(StyleSheet.Parse("* { }").Rules).Selectors[0]
            : StyleSelector.For<DirectNode>();
        Property<int> property = readOnly ? DirectNode.ReadOnlyProperty : DirectNode.ValueProperty;

        Assert.Throws<ArgumentException>(() =>
            new StyleRule(selector, [StyleSetter.Create(property, 3)]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CssRegistrationRejectsDirectProperties(bool readOnly)
    {
        Property<int> property = readOnly ? DirectNode.ReadOnlyProperty : DirectNode.ValueProperty;

        Assert.Throws<ArgumentException>(() =>
            CssRegistry.RegisterProperty<DirectNode, int>("direct-value", property, int.Parse));
    }

    [Fact]
    public void CssConverterCannotIntroduceDirectSetter()
    {
        CssRegistry.RegisterProperty<ConverterNode>("direct-value",
            text => [StyleSetter.Create(DirectNode.ValueProperty, int.Parse(text))]);
        var rule = Assert.Single(StyleSheet.Parse("* { direct-value: 3; }").Rules);

        var error = Assert.Throws<StyleParseException>(() => rule.Bind(typeof(ConverterNode)));

        Assert.IsType<ArgumentException>(error.InnerException);
    }

    [Fact]
    public void StyleSourcesAreEmptyWithoutInitializingCssDefinitions()
    {
        var node = new SourceQueryNode();

        Assert.Empty(node.GetStyleValueSources(DirectNode.ValueProperty));
        Assert.Empty(node.GetStyleValueSources(DirectNode.ReadOnlyProperty));
        CssRegistry.RegisterElement<SourceQueryNode>("DirectSourceQuery");
        Assert.Equal(7, node.GetValue(DirectNode.ValueProperty));
    }

    [Fact]
    public void DirectSetterHonorsDrawingWriteRestriction()
    {
        var node = new DirectNode();
        var screen = new Screen(node);
        Exception? error = null;
        node.DrawAction = () => error = Record.Exception(() => node.Value = 9);
        screen.Open();

        screen.PrepareFrame(new Size(20, 20));
        _ = screen.CreateDrawCommandList();

        Assert.IsType<InvalidOperationException>(error);
        Assert.Equal(7, node.Value);
        screen.Close();
    }

    private class DirectNode : UIElement
    {
        internal static readonly DirectProperty<DirectNode, int> ValueProperty =
            Property.RegisterDirect<DirectNode, int>(nameof(Value), o => o.Value, (o, v) => o.Value = v);

        internal static readonly DirectProperty<DirectNode, int> ReadOnlyProperty =
            Property.RegisterDirect<DirectNode, int>(nameof(ReadOnly), o => o.ReadOnly);

        private int _value = 7;
        internal int ReadOnly => _value;
        internal Action? DrawAction { get; set; }

        internal int Value
        {
            get => _value;
            set => SetField(ValueProperty, ref _value, value);
        }

        protected override Size MeasureCore(Size availableSize) => availableSize;
        protected override void DrawCore(DrawingContext context) => DrawAction?.Invoke();
    }

    private sealed class ConverterNode : DirectNode
    {
    }

    private sealed class SourceQueryNode : DirectNode
    {
    }
}
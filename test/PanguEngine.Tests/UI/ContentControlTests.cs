using PanguEngine.ComponentModel;
using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Styling;

namespace PanguEngine.Tests.UI;

public sealed class ContentControlTests
{
    [Fact]
    public void ContentRootsAreDirectChildrenWithCssRelationshipsAndSingleNotification()
    {
        var root = new Panel();
        var destination = new ContentControl();
        var scene = new UIScene(destination);
        scene.SetStyleSheets([StyleSheet.Parse("ContentControl > Panel { opacity: 0.4; }")]);
        var changes = 0;
        using var subscription = destination.Subscribe(ContentControl.ContentProperty, (_, _) => changes++);

        destination.Content = root;
        destination.UpdateStyles();

        Assert.Same(destination, root.Parent);
        Assert.Equal(0.4, root.Opacity);
        Assert.Equal(1, changes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DetachedPageRejectsWrongThreadClrAndPropertyWrites(bool setValue)
    {
        var root = new Panel();
        var item = new TabItem { Content = root };
        var source = new Source { Value = root };
        item.Bind(TabItem.ContentProperty, source, Source.ValueProperty);
        var scene = new UIScene(item);
        scene.Open();
        Exception? error = null;
        var thread = new Thread(() => error = Record.Exception(() =>
        {
            if (setValue)
                item.SetValue(TabItem.ContentProperty, new Panel());
            else
                item.Content = new Panel();
        }));

        thread.Start();
        thread.Join();

        Assert.IsType<InvalidOperationException>(error);
        Assert.Null(root.Scene);
        Assert.True(item.IsBound(TabItem.ContentProperty));
        Assert.Same(root, item.Content);
        scene.Close();
    }

    [Fact]
    public void SourceNormalizationDuringContentNotificationIsRejectedAsReentry()
    {
        var root = new Panel();
        var replacement = new object();
        var source = new Source { Value = root };
        var control = new ContentControl();
        using var normalization = source.Subscribe(Source.ValueProperty, (_, change) =>
        {
            if (change.NewValue is null)
                source.Value = replacement;
        });
        control.BindTwoWay(ContentControl.ContentProperty, source, Source.ValueProperty);

        Assert.Throws<InvalidOperationException>(() => control.Content = null);

        Assert.Null(control.Content);
        Assert.Same(replacement, source.Value);
        Assert.True(control.IsBound(ContentControl.ContentProperty));
    }

    private sealed class Source : ObservableObject
    {
        internal static readonly Property<object?> ValueProperty =
            Property.Register<Source, object?>(nameof(Value));

        internal object? Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }
    }
}

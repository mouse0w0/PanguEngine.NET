using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Tests.Client.UI;

public sealed class FontSizeInheritanceTests
{
    [Fact]
    public void FontSizeHasOneDescriptorAndDefaultsToSixteenOnEveryNode()
    {
        Assert.Same(Node.FontSizeProperty, Text.FontSizeProperty);
        Assert.Same(Node.FontSizeProperty, Button.FontSizeProperty);
        Assert.Same(Node.FontSizeProperty, TextBox.FontSizeProperty);
        Assert.Equal(typeof(Node), Node.FontSizeProperty.OwnerType);
        Assert.Equal(typeof(Node), Node.FontSizeProperty.TargetType);
        Assert.Equal(16d, Node.FontSizeProperty.DefaultValue);
        Assert.True(Node.FontSizeProperty.Inherits);
        Assert.All(new Node[] { new Panel(), new Text(), new Button(), new TextBox() },
            node => Assert.Equal(16d, node.FontSize));
    }

    [Fact]
    public void DetachedLocalOverrideClearsToDefaultInsteadOfFormerParent()
    {
        var root = new Panel { FontSize = 25 };
        var text = new Text { FontSize = 30 };
        root.Children.Add(text);

        root.Children.Remove(text);
        text.ClearValue(Node.FontSizeProperty);

        Assert.Equal(16d, text.FontSize);
    }

    [Fact]
    public void BindingMasksInheritanceAndUnbindPreservesItsValue()
    {
        var root = new Panel { FontSize = 20 };
        var source = new Text { FontSize = 30 };
        var text = new Text();
        root.Children.Add(text);
        text.Bind(Node.FontSizeProperty, source, Node.FontSizeProperty);

        root.FontSize = 24;
        Assert.Equal(30d, text.FontSize);
        source.FontSize = 32;
        Assert.Equal(32d, text.FontSize);
        text.Unbind(Node.FontSizeProperty);
        source.FontSize = 36;
        root.FontSize = 26;
        Assert.Equal(32d, text.FontSize);

        text.ClearValue(Node.FontSizeProperty);
        Assert.Equal(26d, text.FontSize);
        text.Bind(Node.FontSizeProperty, source, Node.FontSizeProperty);
        text.ClearValue(Node.FontSizeProperty);
        source.FontSize = 40;
        Assert.Equal(26d, text.FontSize);
    }

    [Fact]
    public void CssOnContainersPropagatesAndChildStylesOverrideInheritedLocalValues()
    {
        var root = new Panel { StyleId = "root" };
        var text = new Text();
        var input = new TextBox();
        root.Children.Add(text);
        root.Children.Add(input);
        var screen = new Screen(root);
        screen.SetStyleSheets([StyleSheet.Parse("#root { font-size: 20px; } Text { font-size: 14px; }")]);
        root.UpdateStyles();

        Assert.Equal(20d, root.FontSize);
        Assert.Equal(14d, text.FontSize);
        Assert.Equal(20d, input.FontSize);
        Assert.Empty(input.GetStyleValueSources(Node.FontSizeProperty));

        root.FontSize = 26;
        Assert.Equal(14d, text.FontSize);
        Assert.Equal(26d, input.FontSize);
        text.FontSize = 30;
        Assert.Equal(30d, text.FontSize);
        text.ClearValue(Node.FontSizeProperty);
        Assert.Equal(14d, text.FontSize);
        screen.SetStyleSheets([]);
        root.UpdateStyles();
        Assert.Equal(26d, text.FontSize);
    }

    [Fact]
    public void StyleUpdatesNotifyEachInheritedEffectiveChangeOnce()
    {
        var root = new Panel { StyleId = "root" };
        var middle = new Panel();
        var text = new Text();
        middle.Children.Add(text);
        root.Children.Add(middle);
        var screen = new Screen(root);
        var changes = new List<(Node Node, double Old, double New)>();
        foreach (var node in new Node[] { root, middle, text })
        {
            node.Subscribe(Node.FontSizeProperty, (_, args) =>
            {
                Assert.Equal(24d, root.FontSize);
                Assert.Equal(24d, node.FontSize);
                changes.Add((node, args.OldValue, args.NewValue));
            });
        }

        screen.SetStyleSheets([StyleSheet.Parse("#root { font-size: 24px; }")]);
        Assert.Empty(changes);
        root.UpdateStyles();

        Assert.Equal(3, changes.Count);
        Assert.All(changes, change => Assert.Equal((16d, 24d), (change.Old, change.New)));
        Assert.Equal(3, changes.Select(change => change.Node).Distinct().Count());
    }

    [Fact]
    public void RemovingChildStyleUsesTheUpdatedParentValue()
    {
        var root = new Panel { StyleId = "root" };
        var text = new Text();
        root.Children.Add(text);
        var screen = new Screen(root);
        screen.SetStyleSheets([StyleSheet.Parse("#root { font-size: 20px; } Text { font-size: 14px; }")]);
        root.UpdateStyles();
        var changes = new List<(double Old, double New)>();
        using var subscription = text.Subscribe(Node.FontSizeProperty,
            (_, args) => changes.Add((args.OldValue, args.NewValue)));

        screen.SetStyleSheets([StyleSheet.Parse("#root { font-size: 28px; }")]);
        root.UpdateStyles();
        screen.SetStyleSheets([]);
        root.UpdateStyles();

        Assert.Equal(new[] { (14d, 28d), (28d, 16d) }, changes);
    }

    [Fact]
    public void LaterStyleFailurePreservesEarlierCommitsAndInheritedNotifications()
    {
        var root = new Panel { StyleId = "root" };
        var text = new Text();
        root.Children.Add(text);
        var screen = new Screen(root);
        screen.SetStyleSheets([StyleSheet.Parse("#root { font-size: 20px; }")]);
        root.UpdateStyles();
        var changes = 0;
        using var subscription = text.Subscribe(Node.FontSizeProperty, (_, _) => changes++);

        var invalidSheet = StyleSheet.Parse("#root { font-size: 28px; } Text { font-size: var(--missing); }");
        screen.SetStyleSheets([invalidSheet]);
        Assert.Throws<StyleParseException>(() => root.UpdateStyles());

        Assert.Equal(28d, root.FontSize);
        Assert.Equal(28d, text.FontSize);
        Assert.Equal(1, changes);
        Assert.True(root.IsStyleValid);
        Assert.False(text.IsStyleValid);
        Assert.False(root.IsStyleSubtreeValid);
    }

    [Fact]
    public void StyleNotificationsPropagateReentrantLocalChanges()
    {
        var root = new Panel { StyleId = "root" };
        var text = new Text();
        root.Children.Add(text);
        var screen = new Screen(root);
        using var subscription = root.Subscribe(Node.FontSizeProperty, (_, args) =>
        {
            if (args.NewValue == 24)
                root.FontSize = 30;
        });
        var changes = new List<(double Old, double New)>();
        using var childSubscription = text.Subscribe(Node.FontSizeProperty,
            (_, args) => changes.Add((args.OldValue, args.NewValue)));

        screen.SetStyleSheets([StyleSheet.Parse("#root { font-size: 24px; }")]);
        root.UpdateStyles();

        Assert.Equal(30d, text.FontSize);
        Assert.Equal(new[] { (24d, 30d), (16d, 30d) }, changes);
    }

    [Fact]
    public void ReentrantStyleChangesResumeTheOuterNotification()
    {
        var root = new Panel { StyleId = "root" };
        var text = new Text();
        root.Children.Add(text);
        var screen = new Screen(root);
        var changed = false;
        using var subscription = root.Subscribe(Node.FontSizeProperty, (_, args) =>
        {
            if (args.NewValue != 24 || changed)
                return;
            changed = true;
            root.FontSize = 30;
            root.FontSize = 24;
        });
        var changes = new List<(double Old, double New)>();
        using var childSubscription = text.Subscribe(Node.FontSizeProperty,
            (_, args) => changes.Add((args.OldValue, args.NewValue)));

        screen.SetStyleSheets([StyleSheet.Parse("#root { font-size: 24px; }")]);
        root.UpdateStyles();

        Assert.Equal(24d, text.FontSize);
        Assert.Equal(new[] { (24d, 30d), (30d, 24d), (16d, 24d) }, changes);
    }

    [Fact]
    public void ScreenRootTransferAndRemovalRefreshDescendantFontSizes()
    {
        var root = new Panel { StyleId = "root" };
        var text = new Text();
        root.Children.Add(text);
        var first = new Screen(root);
        first.SetStyleSheets([StyleSheet.Parse("#root { font-size: 20px; }")]);
        root.UpdateStyles();
        var second = new Screen();
        second.SetStyleSheets([StyleSheet.Parse("#root { font-size: 28px; }")]);

        second.Root = root;
        root.UpdateStyles();

        Assert.Null(first.Root);
        Assert.Same(second, text.Screen);
        Assert.Equal(28d, root.FontSize);
        Assert.Equal(28d, text.FontSize);
        second.Root = null;
        root.UpdateStyles();
        Assert.Null(text.Screen);
        Assert.Equal(16d, root.FontSize);
        Assert.Equal(16d, text.FontSize);
    }

    [Fact]
    public void ClearingTwoWayBindingRestoresInheritanceWithoutWritingBack()
    {
        var root = new Panel { FontSize = 20 };
        var text = new Text();
        var source = new Text { FontSize = 30 };
        root.Children.Add(text);
        text.BindTwoWay(Node.FontSizeProperty, source, Node.FontSizeProperty);
        text.FontSize = 32;
        Assert.Equal(32d, source.FontSize);
        root.FontSize = 26;
        Assert.Equal(32d, text.FontSize);

        text.ClearValue(Node.FontSizeProperty);

        Assert.Equal(26d, text.FontSize);
        Assert.Equal(32d, source.FontSize);
        root.FontSize = 28;
        Assert.Equal(28d, text.FontSize);
        Assert.Equal(32d, source.FontSize);
    }

    [Fact]
    public void LaterConvertersObserveAndPreserveEarlierStyleCommits()
    {
        var root = new Panel();
        var text = new Text();
        root.Children.Add(text);
        root.Children.Add(new FontMutationNode());
        var screen = new Screen(root);
        screen.SetStyleSheets([StyleSheet.Parse("Text { font-size: 14px; }")]);
        root.UpdateStyles();
        var changes = new List<(double Old, double New)>();
        using var subscription = text.Subscribe(Node.FontSizeProperty,
            (_, args) => changes.Add((args.OldValue, args.NewValue)));
        FontMutationNode.OnConvert = () =>
        {
            text.FontSize = 25;
            text.ClearValue(Node.FontSizeProperty);
        };
        try
        {
            screen.SetStyleSheets([
                StyleSheet.Parse("Text { font-size: 16px; } FontMutationNode { trigger: 1; }")
            ]);
            root.UpdateStyles();
        }
        finally
        {
            FontMutationNode.OnConvert = null;
        }

        Assert.Equal(16d, text.FontSize);
        Assert.Equal(new[] { (14d, 16d), (16d, 25d), (25d, 16d) }, changes);
    }

    [Fact]
    public void ButtonTextInheritsAndCanOverrideItsFontSizeWithCss()
    {
        var root = new Panel { FontSize = 22 };
        var button = new Button { Text = "Button" };
        root.Children.Add(button);
        var text = Assert.Single(button.ReadOnlyChildren.OfType<Text>());
        Assert.Equal(22d, text.FontSize);
        var screen = new Screen(root);

        screen.SetStyleSheets([StyleSheet.Parse("Button Text { font-size: 14px; }")]);
        root.UpdateStyles();
        Assert.Equal(14d, text.FontSize);
        root.FontSize = 26;
        Assert.Equal(26d, button.FontSize);
        Assert.Equal(14d, text.FontSize);
        screen.SetStyleSheets([]);
        root.UpdateStyles();
        Assert.Equal(26d, text.FontSize);
        button.Text = string.Empty;
        button.Text = "Replacement";
        Assert.Equal(26d, Assert.Single(button.ReadOnlyChildren.OfType<Text>()).FontSize);
    }

    [Fact]
    public void ConvertersCanEstablishLocalValuesBeforeStyleCommit()
    {
        var root = new FontMutationNode();
        var screen = new Screen(root);
        screen.SetStyleSheets([StyleSheet.Parse("FontMutationNode { font-size: 24px; trigger: 1; }")]);
        FontMutationNode.OnConvert = () => root.FontSize = 30;
        try
        {
            root.UpdateStyles();
        }
        finally
        {
            FontMutationNode.OnConvert = null;
        }

        Assert.Equal(30d, root.FontSize);
        Assert.True(Assert.Single(root.GetStyleValueSources(Node.FontSizeProperty)).IsMaskedByLocalValue);
        root.ClearValue(Node.FontSizeProperty);
        Assert.Equal(24d, root.FontSize);
    }

    [Fact]
    public void CommittedStyleChangesNotifyAfterSameValueLocalMask()
    {
        var root = new Panel();
        var child = new Text();
        root.Children.Add(child);
        var screen = new Screen(root);
        screen.SetBaseStyleSheets([]);
        screen.SetStyleSheets([new StyleSheet([
            new StyleRule(StyleSelector.For<Panel>(), [
                StyleSetter.Create(Node.OpacityProperty, 0.5d),
                StyleSetter.Create(Node.FontSizeProperty, 24d)
            ])
        ])]);
        using var opacitySubscription = root.Subscribe(Node.OpacityProperty, (_, _) => root.FontSize = 24);
        var parentNotifications = 0;
        using var parentSubscription = root.Subscribe(Node.FontSizeProperty, (_, _) => parentNotifications++);
        var changes = new List<(double Old, double New)>();
        using var childSubscription = child.Subscribe(Node.FontSizeProperty,
            (_, args) => changes.Add((args.OldValue, args.NewValue)));

        root.UpdateStyles();

        Assert.Equal(24d, root.FontSize);
        Assert.Equal(24d, child.FontSize);
        Assert.Equal(1, parentNotifications);
        Assert.Equal(new[] { (16d, 24d) }, changes);
    }

    [Fact]
    public void MovingDuringStyleNotificationPreservesCommittedInheritedEvents()
    {
        var oldRoot = new Panel();
        var newRoot = new Panel();
        var moving = new Panel { StyleId = "moving" };
        var child = new Text();
        moving.Children.Add(child);
        oldRoot.Children.Add(moving);
        var oldScreen = new Screen(oldRoot);
        var newScreen = new Screen(newRoot);
        var sheet = StyleSheet.Parse("#moving { opacity: 0.5; font-size: 24px; }");
        oldScreen.SetBaseStyleSheets([]);
        newScreen.SetBaseStyleSheets([]);
        oldScreen.SetStyleSheets([sheet]);
        newScreen.SetStyleSheets([sheet]);
        using var opacitySubscription = moving.Subscribe(Node.OpacityProperty,
            (_, _) => newRoot.Children.Add(moving));
        var movingNotifications = 0;
        using var movingSubscription = moving.Subscribe(Node.FontSizeProperty, (_, _) => movingNotifications++);
        var changes = new List<(double Old, double New)>();
        using var childSubscription = child.Subscribe(Node.FontSizeProperty,
            (_, args) => changes.Add((args.OldValue, args.NewValue)));

        oldRoot.UpdateStyles();

        Assert.Same(newRoot, moving.Parent);
        Assert.Equal(24d, moving.FontSize);
        Assert.Equal(24d, child.FontSize);
        Assert.Equal(1, movingNotifications);
        Assert.Equal(new[] { (16d, 24d) }, changes);
        newRoot.UpdateStyles();
        Assert.Equal(24d, child.FontSize);
        Assert.Equal(1, movingNotifications);
        Assert.Equal(new[] { (16d, 24d) }, changes);
    }

    private sealed class FontMutationNode : Node
    {
        internal static Action? OnConvert { get; set; }

        static FontMutationNode()
        {
            CssRegistry.RegisterElement<FontMutationNode>("FontMutationNode");
            CssRegistry.RegisterProperty<FontMutationNode, double>("trigger", OpacityProperty, value =>
            {
                OnConvert?.Invoke();
                return CssValueConverters.ParseNumber(value);
            });
        }
    }
}

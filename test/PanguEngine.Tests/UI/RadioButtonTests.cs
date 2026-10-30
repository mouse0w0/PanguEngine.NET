using PanguEngine.Client.UI;
using PanguEngine.Input;
using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Drawing;
using PanguEngine.UI.Input;
using PanguEngine.UI.Styling;

namespace PanguEngine.Tests.UI;

[Collection(TextServicesCollection.Name)]
public sealed class RadioButtonTests
{
    [Fact]
    public void NonGenericButtonsExposeContentLayoutAndTypedSelectionRequests()
    {
        var numberGroup = new RadioGroup<int?>();
        var textGroup = new RadioGroup<string>();
        var numberButton = new RadioButton<int?> { Group = numberGroup, Value = 1 };
        var textButton = new RadioButton<string> { Group = textGroup, Value = "option" };
        RadioButton[] buttons = [numberButton, textButton];

        foreach (var button in buttons)
        {
            var content = new Panel();
            button.Content = content;
            button.Spacing = 5;
            button.SetValue(RadioButton.IsCheckedProperty, true);
            Assert.Same(content, button.GetValue(RadioButton.ContentProperty));
            Assert.Contains(content, Descendants(button));
            Assert.Equal(5, button.GetValue(RadioButton.SpacingProperty));
            Assert.True(button.IsChecked);
        }

        Assert.Same(numberButton, numberGroup.SelectedButton);
        Assert.Same(textButton, textGroup.SelectedButton);
        foreach (var button in buttons)
            button.IsChecked = false;
        Assert.Null(numberGroup.SelectedButton);
        Assert.Null(textGroup.SelectedButton);
    }

    [Fact]
    public void ContentRetainsOriginalObjectsAndPresentsTheirText()
    {
        var value = new DisplayObject("option");
        var button = new RadioButton<string> { Content = value };
        Assert.Same(value, button.Content);
        Assert.Equal("option", Descendants(button).OfType<Text>().Single().Content);
        button.Content = "replacement";
        Assert.Equal("replacement", Descendants(button).OfType<Text>().Single().Content);
        button.Content = null;
        Assert.Empty(Descendants(button).OfType<Text>());
    }

    [Fact]
    public void EqualContentObjectsWithDifferentReferencesUpdateThePresentation()
    {
        var original = new DisplayObject("option");
        var button = new RadioButton<string> { Content = original };
        var text = Descendants(button).OfType<Text>().Single();
        var changes = 0;
        using var subscription = button.Subscribe(RadioButton<string>.ContentProperty, (_, _) => changes++);

        var replacement = new DisplayObject("option");
        button.SetValue(RadioButton.ContentProperty, replacement);

        Assert.Same(replacement, button.Content);
        Assert.NotSame(text, Descendants(button).OfType<Text>().Single());
        Assert.Equal("option", Descendants(button).OfType<Text>().Single().Content);
        Assert.Equal(1, changes);
        var replacementText = Descendants(button).OfType<Text>().Single();
        button.Content = replacement;
        Assert.Same(replacementText, Descendants(button).OfType<Text>().Single());
        Assert.Equal(1, changes);
    }

    [Fact]
    public void ContentReentryAcceptsOnlyTheCurrentReference()
    {
        var button = new RadioButton<string>();
        var content = new DisplayObject("option");
        using var subscription = button.Subscribe(RadioButton.ContentProperty, (_, _) =>
        {
            button.Content = content;
            Assert.Throws<InvalidOperationException>(() => button.Content = new DisplayObject("option"));
        });

        button.Content = content;

        Assert.Same(content, button.Content);
        Assert.Equal("option", Descendants(button).OfType<Text>().Single().Content);
    }

    [Fact]
    public void ContentNotificationsObserveTheUpdatedPresentation()
    {
        var previous = new Panel();
        var next = new Panel();
        var button = new RadioButton<string> { Content = previous };
        var changes = 0;
        using var subscription = button.Subscribe(RadioButton<string>.ContentProperty, (_, change) =>
        {
            Assert.Same(previous, change.OldValue);
            Assert.Same(next, change.NewValue);
            Assert.Same(next, button.Content);
            Assert.Contains(next, Descendants(button));
            Assert.Null(previous.Parent);
            changes++;
        });

        button.Content = next;

        Assert.Equal(1, changes);
    }

    [Fact]
    public void ContentConversionFailurePreservesTheCommittedPropertyAndReleasesTheGuard()
    {
        var error = new InvalidOperationException("conversion");
        var content = new FailingDisplayObject(error);
        var button = new RadioButton<string>();

        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => button.Content = content));

        Assert.Same(content, button.Content);
        var replacement = new Panel();
        button.Content = replacement;
        Assert.Same(replacement, button.Content);
        Assert.Contains(replacement, Descendants(button));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MovingTheOldNodeDuringConversionDoesNotClearTheNewContent(bool failConversion)
    {
        var previous = new Panel();
        var outside = new Canvas();
        var button = new RadioButton<string> { Content = previous };
        var error = new InvalidOperationException("conversion after migration");
        var next = new CallbackDisplayObject(() =>
        {
            outside.Children.Add(previous);
            if (failConversion)
                throw error;
            return "replacement";
        });

        if (failConversion)
            Assert.Same(error, Assert.Throws<InvalidOperationException>(() => button.Content = next));
        else
            button.Content = next;

        Assert.Same(next, button.Content);
        Assert.Same(outside, previous.Parent);
        if (!failConversion)
            Assert.Equal("replacement", Descendants(button).OfType<Text>().Single().Content);
        var replacement = new Panel();
        button.Content = replacement;
        Assert.Contains(replacement, Descendants(button));
    }

    [Fact]
    public void MovingGeneratedTextClearsTheOriginalContentObject()
    {
        var button = new RadioButton<string> { Content = new DisplayObject("option") };
        var text = Descendants(button).OfType<Text>().Single();
        var outside = new Canvas();

        outside.Children.Add(text);

        Assert.Null(button.Content);
        Assert.Same(outside, text.Parent);
        Assert.Empty(Descendants(button).OfType<Text>());
    }

    [Fact]
    public void MigratingContentClearsTheOldOwnerAndKeepsTheNewNode()
    {
        var content = new StackPanel();
        var oldButton = new RadioButton<string> { Content = content };
        var newButton = new RadioButton<string> { Content = content };
        Assert.Null(oldButton.Content);
        Assert.Same(content, newButton.Content);
        Assert.Contains(content, Descendants(newButton));
        var outside = new Canvas();
        outside.Children.Add(content);
        Assert.Null(newButton.Content);
        Assert.Same(outside, content.Parent);
    }

    [Fact]
    public void ReplacingContentDoesNotClearTheNewObject()
    {
        var first = new Panel();
        var second = new Panel();
        var button = new RadioButton<string> { Content = first };
        button.Content = second;
        Assert.Same(second, button.Content);
        Assert.Null(first.Parent);
        button.Content = second;
        Assert.Contains(second, Descendants(button));
    }

    [Fact]
    public void ContentReentryDuringTreeNotificationsIsRejectedBeforeChangingFields()
    {
        var oldNode = new Panel();
        var newNode = new Panel();
        var button = new RadioButton<string> { Content = oldNode };
        var notifications = 0;
        using var subscription = oldNode.Subscribe(UIElement.ParentProperty, (_, change) =>
        {
            if (change.NewValue is null)
            {
                Assert.Throws<InvalidOperationException>(() => button.Content = null);
                notifications++;
            }
        });

        button.Content = newNode;

        Assert.Equal(1, notifications);
        Assert.Same(newNode, button.Content);
        Assert.Contains(newNode, Descendants(button));
        button.Content = null;
        Assert.Null(button.Content);
        Assert.Null(newNode.Parent);
    }

    [Fact]
    public void ContentNotificationFailuresReleaseTheReentryGuardWithoutRollback()
    {
        var button = new RadioButton<string>();
        var node = new Panel();
        var error = new InvalidOperationException("content observer");
        using (button.Subscribe(RadioButton<string>.ContentProperty, (_, _) => throw error))
            Assert.Same(error, Assert.Throws<InvalidOperationException>(() => button.Content = node));
        Assert.Same(node, button.Content);
        Assert.Contains(node, Descendants(button));
        button.Content = null;
        Assert.Null(node.Parent);
    }

    [Fact]
    public void InvalidContentOwnershipIsRejectedWithoutReplacingTheExistingContent()
    {
        var content = new Panel();
        var button = new RadioButton<string> { Content = content };
        var root = new Canvas();
        root.Children.Add(button);
        Assert.Throws<InvalidOperationException>(() => button.Content = button);
        Assert.Throws<InvalidOperationException>(() => button.Content = root);
        Assert.Same(content, button.Content);
        Assert.Contains(content, Descendants(button));
    }

    [Fact]
    public void TextContentMeasuresWithToolkitAndSelectionDoesNotChangeSize()
    {
        using var context = new TextTestContext();
        var group = new RadioGroup<string>();
        var button = new RadioButton<string> { Group = group, Value = "a", Content = "Option" };
        button.GetStyleRoot().UpdateStyles();
        button.Measure(Size.Infinite);
        var before = button.DesiredSize;
        group.Select(button);
        button.GetStyleRoot().UpdateStyles();
        button.Measure(Size.Infinite);
        Assert.Equal(before, button.DesiredSize);
        Assert.True(before.Width > 0);
        Assert.True(before.Height > 0);
    }

    [Fact]
    public void ContentGetsTheRemainingWidthAndNullRemovesTheGap()
    {
        var content = new Panel { Width = 30, Height = 12 };
        var button = new RadioButton<string> { Content = content, Spacing = 9 };
        button.GetStyleRoot().UpdateStyles();
        button.Measure(Size.Infinite);
        var withContent = button.DesiredSize;
        button.Arrange(new Rect(0, 0, withContent));
        Assert.True(content.LayoutBounds.Width > 0);
        button.Content = null;
        button.Measure(Size.Infinite);
        Assert.Equal(39, withContent.Width - button.DesiredSize.Width, 10);
    }

    [Fact]
    public void FiniteMeasurementSubtractsIndicatorSpacingAndContentMarginExactlyOnce()
    {
        var content = new MeasurementProbe { Margin = new Thickness(2, 0) };
        var button = new RadioButton<string>
        {
            Content = content, Spacing = 5, Padding = Thickness.Zero, BorderThickness = Thickness.Zero
        };
        button.GetStyleRoot().UpdateStyles();

        button.Measure(new Size(40, 20));
        button.Arrange(new Rect(0, 0, 40, 20));

        Assert.Equal(new Size(13, 20), content.AvailableSize);
        Assert.Equal(13, content.LayoutBounds.Width, 10);
        button.Measure(Size.Zero);
        button.Arrange(Rect.Zero);
        Assert.Equal(Size.Zero, content.AvailableSize);
        Assert.Equal(0, button.LayoutBounds.Width);
        Assert.True(content.LayoutBounds.Width >= 0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SpacingUsesTheSceneScaleAndLayoutRoundingPolicy(bool rounding)
    {
        var content = new Panel { Width = 15, Height = 12, HorizontalAlignment = HorizontalAlignment.Left };
        var button = new RadioButton<string>
        {
            Content = content, Spacing = 1.1, Padding = Thickness.Zero, BorderThickness = Thickness.Zero
        };
        _ = new UIScene(button) { Scale = 1.5, UseLayoutRounding = rounding };
        button.GetStyleRoot().UpdateStyles();
        button.Measure(new Size(100, 30));
        button.Arrange(new Rect(0, 0, 100, 30));
        var indicator = Descendants(button).Single(node => node.Classes.Contains("radio-button-indicator"));
        var gap = content.LocalToScene(new Point(0, 0)).X -
                  indicator.LocalToScene(new Point(0, 0)).X - indicator.LayoutBounds.Width;
        Assert.Equal(rounding ? 2d / 1.5 : 1.1, gap, 10);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DefaultCheckedAndDisabledStylesAffectTheIndicatorAndGeneratedText(bool disableAncestor)
    {
        var group = new RadioGroup<string>();
        var button = new RadioButton<string> { Group = group, Value = "a", Content = "option" };
        var root = new StackPanel();
        root.Children.Add(button);
        button.GetStyleRoot().UpdateStyles();
        var ring = Descendants(button).OfType<Ellipse>().Single(node => node.Classes.Contains("radio-button-ring"));
        var dot = Descendants(button).OfType<Ellipse>().Single(node => node.Classes.Contains("radio-button-dot"));
        var text = Descendants(button).OfType<Text>().Single();
        Assert.Null(dot.Fill);
        Assert.Equal("stroke-width",
            Assert.Single(ring.GetStyleValueSources(Shape.StrokeThicknessProperty)).CssPropertyName);
        Assert.Equal(16, text.FontSize);
        group.Select(button);
        button.GetStyleRoot().UpdateStyles();
        Assert.Equal(new SolidColorBrush(84, 169, 255), dot.Fill);
        button.SetHovered(true);
        button.SetPressed(true);
        if (disableAncestor)
            root.IsEnabled = false;
        else
            button.IsEnabled = false;
        button.GetStyleRoot().UpdateStyles();
        Assert.Equal(new SolidColorBrush(52, 58, 65), ring.Stroke);
        Assert.Equal(new SolidColorBrush(110, 120, 132), dot.Fill);
        Assert.Equal(new Color(139, 148, 160), text.Color);
    }

    [Fact]
    public void CheckedStyleDoesNotSelectAnUnselectedRadioInsideContent()
    {
        var group = new RadioGroup<string>();
        var inner = new RadioButton<string>();
        var outer = new RadioButton<string> { Group = group, Value = "outer", Content = inner };
        group.Select(outer);
        outer.GetStyleRoot().UpdateStyles();
        var dot = Descendants(inner).OfType<Ellipse>().Single(node => node.Classes.Contains("radio-button-dot"));
        Assert.False(inner.IsChecked);
        Assert.Null(dot.Fill);
    }

    [Fact]
    public void CheckedStylesApplyToEveryClosedGenericType()
    {
        var firstGroup = new RadioGroup<int?>();
        var secondGroup = new RadioGroup<string>();
        var first = new RadioButton<int?> { Group = firstGroup, Value = 1 };
        var second = new RadioButton<string> { Group = secondGroup, Value = "a" };
        var root = new StackPanel();
        root.Children.Add(first);
        root.Children.Add(second);
        var scene = new UIScene(root);
        scene.SetStyleSheets([
            StyleSheet.Parse("RadioButton { spacing: 5px; } RadioButton:checked { background-color: #010203; }")
        ]);
        firstGroup.Select(first);
        secondGroup.Select(second);
        root.UpdateStyles();
        Assert.Equal(5, first.Spacing);
        Assert.Equal(5, second.Spacing);
        Assert.Equal(new SolidColorBrush(1, 2, 3), first.Background);
        Assert.Equal(new SolidColorBrush(1, 2, 3), second.Background);
        firstGroup.Clear();
        root.UpdateStyles();
        Assert.NotEqual(new SolidColorBrush(1, 2, 3), first.Background);
    }

    [Fact]
    public void MouseSelectsOnCompletedLeftClickAndKeepsAnAlreadySelectedButton()
    {
        var (manager, _, _, group, buttons) = OpenScene();
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        Assert.Null(group.SelectedButton);
        manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        Assert.Same(buttons[0], group.SelectedButton);
        Click(manager, new Point(5, 5));
        Assert.Same(buttons[0], group.SelectedButton);
        manager.Close();
    }

    [Fact]
    public void OutsideReleaseAndRightClickDoNotSelect()
    {
        var (manager, _, _, group, _) = OpenScene();
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerReleased(new Point(250, 150), MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Right, KeyModifiers.None);
        manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Right, KeyModifiers.None);
        Assert.Null(group.SelectedButton);
        manager.Close();
    }

    [Fact]
    public void SpaceRequiresAPairedReleaseAndEnterIgnoresRepeats()
    {
        var (manager, _, _, group, buttons) = OpenScene();
        Assert.True(buttons[0].Focus());
        manager.ProcessKeyUp(Key.Space, KeyModifiers.None);
        Assert.Null(group.SelectedButton);
        manager.ProcessKeyDown(Key.Space, KeyModifiers.None);
        Assert.Null(group.SelectedButton);
        manager.ProcessKeyDown(Key.Space, KeyModifiers.None, true);
        manager.ProcessKeyUp(Key.Space, KeyModifiers.None);
        Assert.Same(buttons[0], group.SelectedButton);
        group.Clear();
        manager.ProcessKeyDown(Key.Enter, KeyModifiers.None);
        Assert.Same(buttons[0], group.SelectedButton);
        group.Clear();
        manager.ProcessKeyDown(Key.Enter, KeyModifiers.None, true);
        Assert.Null(group.SelectedButton);
        manager.ProcessKeyUp(Key.Enter, KeyModifiers.None);
        manager.Close();
    }

    [Fact]
    public void FocusLossCancelsSpaceAndRepeatDoesNotReestablishThePair()
    {
        var (manager, scene, _, group, buttons) = OpenScene();
        Assert.True(buttons[0].Focus());
        manager.ProcessKeyDown(Key.Space, KeyModifiers.None);
        scene.ClearFocus();
        Assert.True(buttons[0].Focus());
        manager.ProcessKeyDown(Key.Space, KeyModifiers.None, true);
        manager.ProcessKeyUp(Key.Space, KeyModifiers.None);
        Assert.Null(group.SelectedButton);
        manager.Close();
    }

    [Theory]
    [InlineData(Key.Enter)]
    [InlineData(Key.Space)]
    public void FocusLossAndReturnDuringAKeyCallbackCancelTheActivation(Key key)
    {
        var (manager, scene, _, group, buttons) = OpenScene();
        Assert.True(buttons[0].Focus());

        void LoseAndReturnFocus(object? sender, KeyEventArgs args)
        {
            if (args.Key == key)
            {
                scene.ClearFocus();
                Assert.True(buttons[0].Focus());
            }
        }

        if (key == Key.Enter)
            buttons[0].KeyDown += LoseAndReturnFocus;
        else
            buttons[0].KeyUp += LoseAndReturnFocus;
        manager.ProcessKeyDown(key, KeyModifiers.None);
        manager.ProcessKeyUp(key, KeyModifiers.None);
        Assert.Null(group.SelectedButton);
        Assert.False(buttons[0].HasPseudoClass(PseudoClass.Pressed));
        manager.Close();
    }

    [Fact]
    public void SpacePressedStyleIsSeparateFromMousePressedAndClearsOnFocusLoss()
    {
        var (manager, scene, _, _, buttons) = OpenScene();
        Assert.True(buttons[0].Focus());
        manager.ProcessKeyDown(Key.Space, KeyModifiers.None);
        Assert.False(buttons[0].IsPressed);
        Assert.True(buttons[0].HasPseudoClass(PseudoClass.Pressed));
        scene.ClearFocus();
        Assert.False(buttons[0].HasPseudoClass(PseudoClass.Pressed));
        manager.Close();
    }

    [Fact]
    public void RawKeyHandlersCanHandleOrRemoveTheButtonBeforeSelection()
    {
        var (manager, _, root, group, buttons) = OpenScene();
        Assert.True(buttons[0].Focus());
        buttons[0].KeyDown += (_, args) => args.Handled = true;
        manager.ProcessKeyDown(Key.Enter, KeyModifiers.None);
        Assert.Null(group.SelectedButton);
        Assert.True(buttons[1].Focus());
        buttons[1].KeyDown += (_, _) => root.Children.Remove(buttons[1]);
        manager.ProcessKeyDown(Key.Enter, KeyModifiers.None);
        Assert.Null(group.SelectedButton);
        manager.Close();
    }

    [Fact]
    public void ArrowNavigationUsesRegistrationOrderSkipsUnavailableMembersAndWraps()
    {
        var (manager, _, _, group, buttons) = OpenScene();
        buttons[1].IsEnabled = false;
        Assert.True(buttons[0].Focus());
        manager.ProcessKeyDown(Key.Right, KeyModifiers.None);
        Assert.True(buttons[2].IsFocused);
        Assert.Same(buttons[2], group.SelectedButton);
        manager.ProcessKeyDown(Key.Down, KeyModifiers.None);
        Assert.True(buttons[0].IsFocused);
        Assert.Same(buttons[0], group.SelectedButton);
        manager.ProcessKeyDown(Key.Left, KeyModifiers.None);
        Assert.Same(buttons[2], group.SelectedButton);
        manager.Close();
    }

    [Fact]
    public void ModifiedArrowsAndDisabledAncestorsDoNotNavigateOrSelect()
    {
        var (manager, _, root, group, buttons) = OpenScene();
        Assert.True(buttons[0].Focus());
        manager.ProcessKeyDown(Key.Right, KeyModifiers.Shift);
        Assert.True(buttons[0].IsFocused);
        Assert.Null(group.SelectedButton);
        buttons[0].KeyDown += (_, _) => root.IsEnabled = false;
        manager.ProcessKeyDown(Key.Enter, KeyModifiers.None);
        Assert.Null(group.SelectedButton);
        Assert.False(buttons[0].IsFocused);
        manager.Close();
    }

    [Fact]
    public void NavigationSkipsDetachedAndOtherSceneMembers()
    {
        var (manager, _, root, group, buttons) = OpenScene();
        root.Children.Remove(buttons[1]);
        _ = new UIScene(buttons[1]);
        buttons[2].Visibility = Visibility.Hidden;
        Assert.True(buttons[0].Focus());
        manager.ProcessKeyDown(Key.Right, KeyModifiers.None);
        Assert.True(buttons[0].IsFocused);
        Assert.Null(group.SelectedButton);
        manager.Close();
    }

    [Fact]
    public void FocusCallbacksCanChangeMembershipWithoutSelectingTheOldCandidate()
    {
        var (manager, _, _, group, buttons) = OpenScene();
        buttons[1].GotFocus += (_, _) => buttons[1].Group = null;
        Assert.True(buttons[0].Focus());
        manager.ProcessKeyDown(Key.Right, KeyModifiers.None);
        Assert.Null(group.SelectedButton);
        Assert.Null(buttons[1].Group);
        manager.Close();
    }

    [Fact]
    public void InteractiveContentHandlesItsOwnClickWithoutSelectingTheRadio()
    {
        var (manager, _, _, group, buttons) = OpenScene();
        var inner = new Button { Width = 35, Height = 20 };
        var clicks = 0;
        inner.Click += (_, _) => clicks++;
        buttons[0].Content = inner;
        manager.UpdateFrame(new Size(300, 180), 0);
        var point = ToScene(inner, new Point(5, 5));
        Click(manager, point);
        Assert.Equal(1, clicks);
        Assert.Null(group.SelectedButton);
        Assert.True(inner.IsFocused);
        manager.Close();
    }

    private static (UIManager Manager, GameUIScene Scene, Canvas Root,
        RadioGroup<int?> Group, RadioButton<int?>[] Buttons) OpenScene()
    {
        var group = new RadioGroup<int?>();
        var root = new Canvas();
        var buttons = Enumerable.Range(0, 3).Select(index => new RadioButton<int?>
        {
            Group = group, Value = index, Width = 100, Height = 32
        }).ToArray();
        for (var index = 0; index < buttons.Length; index++)
        {
            Canvas.SetTop(buttons[index], index * 40);
            root.Children.Add(buttons[index]);
        }

        var scene = new GameUIScene(root);
        var manager = new UIManager();
        manager.Open(scene);
        manager.UpdateFrame(new Size(300, 180), 0);
        return (manager, scene, root, group, buttons);
    }

    private static void Click(UIManager manager, Point point)
    {
        manager.ProcessPointerPressed(point, MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerReleased(point, MouseButton.Left, KeyModifiers.None);
    }

    private static Point ToScene(UIElement node, Point local)
    {
        for (UIElement? current = node; current is not null; current = current.Parent)
            local = new Point(local.X + current.LayoutBounds.X, local.Y + current.LayoutBounds.Y);
        return local;
    }

    private static IEnumerable<UIElement> Descendants(Container parent)
    {
        foreach (var child in parent.ReadOnlyChildren)
        {
            yield return child;
            if (child is Container branch)
                foreach (var descendant in Descendants(branch))
                    yield return descendant;
        }
    }

    private sealed class MeasurementProbe : Control
    {
        internal Size AvailableSize { get; private set; }

        protected override Size MeasureContent(Size availableSize)
        {
            AvailableSize = availableSize;
            return new Size(Math.Min(30, availableSize.Width), Math.Min(12, availableSize.Height));
        }
    }

    private sealed class FailingDisplayObject(Exception error)
    {
        public override string ToString() => throw error;
    }

    private sealed class CallbackDisplayObject(Func<string> format)
    {
        public override string ToString() => format();
    }

    private sealed record DisplayObject(string Label)
    {
        public override string ToString() => Label;
    }
}

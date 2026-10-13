using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Styling;
using PanguEngine.Input;

namespace PanguEngine.Tests.Client.UI;

[Collection(TextServicesCollection.Name)]
public sealed class CheckBoxTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, null, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, null)]
    [InlineData(true, null, false)]
    public void UserActivationFollowsTheCompleteStateTable(bool threeState, bool? initial, bool? expected)
    {
        var box = new CheckBox { IsThreeState = threeState, IsChecked = initial };
        var (manager, _) = OpenScene(box);
        Click(manager);
        Assert.Equal(expected, box.IsChecked);
        manager.Close();
    }

    [Fact]
    public void ProgrammaticChangesKeepIndeterminateAndDoNotActivate()
    {
        var box = new CheckBox { IsThreeState = true, IsChecked = null };
        var clicks = 0;
        box.Click += (_, _) => clicks++;
        box.IsThreeState = false;
        Assert.Null(box.IsChecked);
        box.IsChecked = true;
        box.IsChecked = null;
        Assert.Equal(0, clicks);
        Assert.True(box.HasPseudoClass(UiPseudoClass.Indeterminate));
        Assert.False(box.HasPseudoClass(UiPseudoClass.Checked));
    }

    [Fact]
    public void StateNotificationPrecedesActivationAndCarriesTheNewPseudoClass()
    {
        var box = new CheckBox();
        var (manager, _) = OpenScene(box);
        var calls = new List<string>();
        using var subscription = box.Subscribe(CheckBox.IsCheckedProperty, (_, args) =>
        {
            Assert.Equal(true, args.NewValue);
            Assert.True(box.HasPseudoClass(UiPseudoClass.Checked));
            calls.Add("state");
        });
        box.Click += (_, _) => calls.Add("click");
        Click(manager);
        Assert.Equal(["state", "click"], calls);
        manager.Close();
    }

    [Fact]
    public void UserChangeWritesBackToTwoWayBinding()
    {
        var source = new CheckBox { IsChecked = null };
        var box = new CheckBox();
        box.BindTwoWay(CheckBox.IsCheckedProperty, source, CheckBox.IsCheckedProperty);
        var (manager, _) = OpenScene(box);
        Click(manager);
        Assert.Equal(false, source.IsChecked);
        Assert.True(box.IsBound(CheckBox.IsCheckedProperty));
        source.IsChecked = true;
        Assert.Equal(true, box.IsChecked);
        manager.Close();
    }

    [Fact]
    public void UserChangeKeepsOneWayBindingWithoutWritingBack()
    {
        var source = new CheckBox();
        var box = new CheckBox();
        box.Bind(CheckBox.IsCheckedProperty, source, CheckBox.IsCheckedProperty);
        var (manager, _) = OpenScene(box);
        Click(manager);
        Assert.Equal(true, box.IsChecked);
        Assert.Equal(false, source.IsChecked);
        Assert.True(box.IsBound(CheckBox.IsCheckedProperty));
        source.IsChecked = null;
        Assert.Null(box.IsChecked);
        manager.Close();
    }

    [Fact]
    public void EqualContentDoesNotReformatAndObjectMutationDoesNotRefresh()
    {
        var value = new LabelValue { Value = "first" };
        var box = new CheckBox { Content = value };
        Assert.Equal("first", AutomaticText(box).Content);
        value.Value = "second";
        box.Content = value;
        Assert.Equal(1, value.Calls);
        Assert.Equal("first", AutomaticText(box).Content);
        box.Content = "replacement";
        Assert.Equal("replacement", AutomaticText(box).Content);
    }

    [Fact]
    public void FormattingContentCanReplaceTheLabelWithoutRemovingTheReplacement()
    {
        var box = new CheckBox { Content = "initial" };
        box.Content = new ReentrantLabel(() => box.Content = "replacement");
        Assert.Equal("replacement", box.Content);
        Assert.Equal("replacement", AutomaticText(box).Content);
    }

    [Fact]
    public void EmptyAndNullFormattedContentDoNotReserveLabelSpacing()
    {
        var box = new CheckBox();
        box.UpdateStyles();
        box.Measure(Size.Infinite);
        var emptySize = box.DesiredSize;
        foreach (var content in new object?[] { "", new LabelValue(), null })
        {
            box.Content = content;
            box.UpdateStyles();
            box.Measure(Size.Infinite);
            Assert.Equal(emptySize, box.DesiredSize);
        }
    }

    [Fact]
    public void ContentMigratesAndClearsThePreviousSlot()
    {
        var content = new Panel { Width = 20, Height = 10 };
        var first = new CheckBox { Content = content };
        var second = new CheckBox { Content = content };
        Assert.Null(first.Content);
        Assert.Same(content, second.Content);
        var destination = new Panel();
        destination.Children.Add(content);
        Assert.Null(second.Content);
        Assert.Same(destination, content.Parent);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MigratingBoundContentPreservesBindingAndOnlyWritesBackWhenTwoWay(bool twoWay)
    {
        var content = new Panel();
        var source = new ContentSource { Value = content };
        var box = new CheckBox();
        if (twoWay)
            box.BindTwoWay(CheckBox.ContentProperty, source, item => item.Value);
        else
            box.Bind(CheckBox.ContentProperty, source, item => item.Value);
        var destination = new Panel();
        destination.Children.Add(content);
        Assert.Null(box.Content);
        Assert.True(box.IsBound(CheckBox.ContentProperty));
        if (twoWay)
            Assert.Null(source.Value);
        else
            Assert.Same(content, source.Value);
        Assert.Same(destination, content.Parent);
    }

    [Fact]
    public void InvalidContentDoesNotDetachTheExistingLabel()
    {
        var label = new Panel();
        var box = new CheckBox { Content = label };
        var root = new Panel();
        root.Children.Add(box);
        var parent = label.Parent;
        Assert.Throws<ArgumentException>(() => box.Content = box);
        Assert.Throws<ArgumentException>(() => box.Content = root);
        Assert.Same(label, box.Content);
        Assert.Same(parent, label.Parent);
    }

    [Fact]
    public void LabelNodesDoNotHandlePointerInputOrHaveTheirSettingsRewritten()
    {
        var nested = new Button { Width = 60, Height = 20 };
        var box = new CheckBox { Content = nested };
        var (manager, _) = OpenScene(box);
        var nestedClicks = 0;
        var clicks = 0;
        nested.Click += (_, _) => nestedClicks++;
        box.Click += (_, _) => clicks++;
        Click(manager, new Point(50, 15));
        Assert.Equal(1, clicks);
        Assert.Equal(0, nestedClicks);
        Assert.True(nested.IsHitTestVisible);
        Assert.True(nested.Focusable);
        Assert.True(box.IsFocused);
        manager.Close();
    }

    [Fact]
    public void OutsideReleaseAndRightClickDoNotToggle()
    {
        var box = new CheckBox();
        var (manager, _) = OpenScene(box);
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerReleased(new Point(150, 70), MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Right, KeyModifiers.None);
        manager.ProcessPointerReleased(new Point(5, 5), MouseButton.Right, KeyModifiers.None);
        Assert.Equal(false, box.IsChecked);
        manager.Close();
    }

    [Fact]
    public void SpaceActivatesOnceOnReleaseAndEnterDoesNotActivate()
    {
        var box = new CheckBox();
        var (manager, _) = OpenScene(box);
        Assert.True(box.Focus());
        manager.ProcessKeyDown(Key.Enter, KeyModifiers.None);
        manager.ProcessKeyUp(Key.Enter, KeyModifiers.None);
        Assert.Equal(false, box.IsChecked);
        manager.ProcessKeyDown(Key.Space, KeyModifiers.None);
        manager.ProcessKeyDown(Key.Space, KeyModifiers.None, isRepeat: true);
        Assert.Equal(false, box.IsChecked);
        Assert.True(box.HasPseudoClass(UiPseudoClass.Pressed));
        manager.ProcessKeyUp(Key.Space, KeyModifiers.None);
        manager.ProcessKeyUp(Key.Space, KeyModifiers.None);
        Assert.Equal(true, box.IsChecked);
        Assert.False(box.HasPseudoClass(UiPseudoClass.Pressed));
        manager.Close();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LosingFocusOrDisablingCancelsPendingSpace(bool disable)
    {
        var box = new CheckBox();
        var (manager, screen) = OpenScene(box);
        Assert.True(box.Focus());
        manager.ProcessKeyDown(Key.Space, KeyModifiers.None);
        if (disable)
            box.IsEnabled = false;
        else
            screen.ClearFocus();
        manager.ProcessKeyUp(Key.Space, KeyModifiers.None);
        Assert.Equal(false, box.IsChecked);
        Assert.False(box.HasPseudoClass(UiPseudoClass.Pressed));
        manager.Close();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RepeatedSpaceDoesNotRestartAnActivationCancelledByFocusLossOrDisabling(bool disable)
    {
        var box = new CheckBox();
        var (manager, screen) = OpenScene(box);
        var clicks = 0;
        box.Click += (_, _) => clicks++;
        Assert.True(box.Focus());
        manager.ProcessKeyDown(Key.Space, KeyModifiers.None);
        if (disable)
        {
            box.IsEnabled = false;
            box.IsEnabled = true;
        }
        else
            screen.ClearFocus();
        Assert.True(box.Focus());
        manager.ProcessKeyDown(Key.Space, KeyModifiers.None, isRepeat: true);
        Assert.False(box.HasPseudoClass(UiPseudoClass.Pressed));
        manager.ProcessKeyUp(Key.Space, KeyModifiers.None);
        Assert.Equal(false, box.IsChecked);
        Assert.Equal(0, clicks);
        manager.ProcessKeyDown(Key.Space, KeyModifiers.None);
        manager.ProcessKeyUp(Key.Space, KeyModifiers.None);
        Assert.Equal(true, box.IsChecked);
        Assert.Equal(1, clicks);
        manager.Close();
    }

    [Fact]
    public void RawReleaseCallbackCanCancelKeyboardActivation()
    {
        var box = new CheckBox();
        var (manager, _) = OpenScene(box);
        Assert.True(box.Focus());
        box.KeyUp += (_, _) => box.IsEnabled = false;
        manager.ProcessKeyDown(Key.Space, KeyModifiers.None);
        manager.ProcessKeyUp(Key.Space, KeyModifiers.None);
        Assert.Equal(false, box.IsChecked);
        manager.Close();
    }

    [Fact]
    public void LosingAndRegainingFocusInsideKeyUpStillCancelsActivation()
    {
        var box = new CheckBox();
        var (manager, screen) = OpenScene(box);
        Assert.True(box.Focus());
        box.KeyUp += (_, _) =>
        {
            screen.ClearFocus();
            Assert.True(box.Focus());
        };
        manager.ProcessKeyDown(Key.Space, KeyModifiers.None);
        manager.ProcessKeyUp(Key.Space, KeyModifiers.None);
        Assert.Equal(false, box.IsChecked);
        manager.Close();
    }

    [Fact]
    public void RemovingIncomingContentCanReplaceTheDestinationWithoutLeavingTwoLabels()
    {
        var incoming = new Panel();
        var replacement = new Panel();
        var source = new Panel();
        source.Children.Add(incoming);
        var box = new CheckBox();
        source.Children.Changed += (_, _) => box.Content = replacement;
        box.Content = incoming;
        Assert.Same(replacement, box.Content);
        Assert.NotNull(replacement.Parent);
        Assert.Single(replacement.Parent.ReadOnlyChildren);
        Assert.Null(incoming.Parent);
    }

    [Fact]
    public void ContentCanMoveFromAScreenRoot()
    {
        var content = new Panel();
        var source = new UiScreen(content);
        var box = new CheckBox { Content = content };
        Assert.Null(source.Root);
        Assert.Same(content, box.Content);
        Assert.NotNull(content.Parent);
    }

    [Fact]
    public void AuthorStylesControlGeneratedTextWithoutLocalValueMasks()
    {
        var box = new CheckBox { Content = "label" };
        var screen = new UiScreen(box);
        screen.SetStyleSheets([UiStyleSheet.Parse("CheckBox Text.checkbox-text { font-size: 23px; color: #123456; }")]);
        box.UpdateStyles();
        var text = AutomaticText(box);
        Assert.Equal(23, text.FontSize);
        Assert.Equal(new PanguEngine.Client.UI.Drawing.Color(18, 52, 86), text.Color);
        Assert.All(text.GetStyleValueSources(Text.FontSizeProperty), source => Assert.False(source.IsMaskedByLocalValue));
    }

    [Fact]
    public void StateChangesKeepTheSameLayoutSize()
    {
        var box = new CheckBox { Content = new Panel { Width = 40, Height = 12 } };
        box.UpdateStyles();
        box.Measure(Size.Infinite);
        var size = box.DesiredSize;
        foreach (var state in new bool?[] { true, null, false })
        {
            box.IsChecked = state;
            box.UpdateStyles();
            box.Measure(Size.Infinite);
            Assert.Equal(size, box.DesiredSize);
        }
    }

    [Fact]
    public void CollapsedContentDropsItsSpacingAndMarginParticipatesInLayout()
    {
        var content = new Panel { Width = 40, Height = 10, Margin = new Thickness(3) };
        var box = new CheckBox { Content = content, Spacing = 9 };
        box.UpdateStyles();
        box.Measure(Size.Infinite);
        var withContent = box.DesiredSize;
        content.Visibility = Visibility.Collapsed;
        box.Measure(Size.Infinite);
        var collapsed = box.DesiredSize;
        Assert.Equal(46 + 9, withContent.Width - collapsed.Width);
        content.Visibility = Visibility.Hidden;
        box.Measure(Size.Infinite);
        Assert.Equal(withContent, box.DesiredSize);
    }

    private static (UiManager Manager, GameScreen Screen) OpenScene(CheckBox box)
    {
        box.Width = 120;
        box.Height = 32;
        var root = new Canvas();
        root.Children.Add(box);
        var manager = new UiManager();
        var screen = new GameScreen(root) { UseLayoutRounding = false };
        manager.Open(screen);
        manager.UpdateFrame(new Size(200, 100), 0);
        return (manager, screen);
    }

    private static void Click(UiManager manager, Point? position = null)
    {
        var point = position ?? new Point(5, 5);
        manager.ProcessPointerPressed(point, MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerReleased(point, MouseButton.Left, KeyModifiers.None);
    }

    private static Text AutomaticText(CheckBox box) =>
        Descendants(box).OfType<Text>().Single(text => text.Classes.Contains("checkbox-text"));

    private static IEnumerable<UiNode> Descendants(Parent parent)
    {
        foreach (var child in parent.ReadOnlyChildren)
        {
            yield return child;
            if (child is Parent nested)
                foreach (var descendant in Descendants(nested))
                    yield return descendant;
        }
    }

    private sealed class ReentrantLabel(Action format)
    {
        public override string ToString()
        {
            format();
            return "obsolete";
        }
    }

    private sealed class LabelValue
    {
        public string? Value { get; set; }
        public int Calls { get; private set; }
        public override string? ToString()
        {
            Calls++;
            return Value;
        }
    }

    private sealed class ContentSource : System.ComponentModel.INotifyPropertyChanged
    {
        private object? _value;
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        public object? Value
        {
            get => _value;
            set
            {
                if (Equals(_value, value))
                    return;
                _value = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Value)));
            }
        }
    }
}

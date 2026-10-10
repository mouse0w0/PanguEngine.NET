using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Styling;
using PanguEngine.ComponentModel;
using PanguEngine.Input;

namespace PanguEngine.Tests.Client.UI;

public sealed class ToggleButtonTests
{
    [Fact]
    public void DefaultsAndStyleRegistrationMatchTheToggleContract()
    {
        var button = new ToggleButton();
        button.UpdateStyles();

        Assert.Equal(typeof(Button), typeof(ToggleButton).BaseType);
        Assert.True(typeof(ToggleButton).IsSealed);
        Assert.Equal(typeof(ToggleButton), ToggleButton.IsCheckedProperty.OwnerType);
        Assert.Equal(typeof(bool), ToggleButton.IsCheckedProperty.ValueType);
        Assert.Equal(false, ToggleButton.IsCheckedProperty.DefaultValue);
        Assert.False(ToggleButton.IsCheckedProperty.IsReadOnly);
        Assert.False(button.IsChecked);
        Assert.True(button.Focusable);
        Assert.False(button.HasPseudoClass(UiPseudoClass.Get("checked")));
        Assert.Equal("Button", BackgroundSelector(button));

        var screen = new UiScreen(button);
        screen.SetStyleSheets([UiStyleSheet.Parse("ToggleButton { spacing: 11px; }")]);
        button.UpdateStyles();

        Assert.Equal(11d, button.Spacing);
    }

    [Fact]
    public void ExternalWritesAndClearValueNotifyWithoutClicking()
    {
        var button = new ToggleButton();
        var changes = new List<bool>();
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        using var subscription = button.Subscribe(ToggleButton.IsCheckedProperty, (_, args) =>
        {
            Assert.Equal(args.NewValue, button.HasPseudoClass(UiPseudoClass.Get("checked")));
            changes.Add(args.NewValue);
        });

        button.IsChecked = true;
        button.IsChecked = true;
        button.ClearValue(ToggleButton.IsCheckedProperty);

        Assert.Equal(new[] { true, false }, changes);
        Assert.False(button.IsChecked);
        Assert.Equal(0, clicks);
    }

    [Fact]
    public void PointerActivationTogglesBeforeClick()
    {
        var (manager, screen, button) = OpenScene();
        var observed = new List<bool>();
        button.Click += (_, _) => observed.Add(button.IsChecked);

        Click(manager);
        Click(manager);

        Assert.Equal(new[] { true, false }, observed);
        Assert.False(button.IsChecked);
        manager.Close();
    }

    [Theory]
    [InlineData(Key.Enter)]
    [InlineData(Key.Space)]
    public void KeyboardActivationUsesExistingTimingAndIgnoresRepeats(Key key)
    {
        var (manager, screen, button) = OpenScene();
        var observed = new List<bool>();
        button.Click += (_, _) => observed.Add(button.IsChecked);
        Assert.True(button.Focus());

        manager.ProcessKeyDown(key, KeyModifiers.None);
        manager.ProcessKeyDown(key, KeyModifiers.None, isRepeat: true);
        Assert.Equal(key == Key.Enter, button.IsChecked);
        Assert.Equal(key == Key.Enter ? 1 : 0, observed.Count);
        if (key == Key.Space)
            Assert.True(button.HasPseudoClass(UiPseudoClass.Pressed));
        manager.ProcessKeyUp(key, KeyModifiers.None);
        manager.ProcessKeyUp(key, KeyModifiers.None);

        Assert.Equal(new[] { true }, observed);
        manager.ProcessKeyDown(key, KeyModifiers.None);
        manager.ProcessKeyUp(key, KeyModifiers.None);
        Assert.Equal(new[] { true, false }, observed);
        manager.Close();
    }

    [Fact]
    public void OtherPointerButtonsAndReleaseOutsideDoNotToggle()
    {
        var (manager, screen, button) = OpenScene();
        var clicks = 0;
        button.Click += (_, _) => clicks++;

        Click(manager, MouseButton.Right);
        Click(manager, MouseButton.Middle);
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerReleased(new Point(90, 90), MouseButton.Left, KeyModifiers.None);

        Assert.False(button.IsChecked);
        Assert.Equal(0, clicks);
        manager.Close();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FocusLossOrDisableCancelsSpaceWithoutClearingSelection(bool disable)
    {
        var (manager, screen, button) = OpenScene();
        button.IsChecked = true;
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        Assert.True(button.Focus());
        manager.ProcessKeyDown(Key.Space, KeyModifiers.None);

        if (disable)
            button.IsEnabled = false;
        else
            screen.ClearFocus();
        manager.ProcessKeyUp(Key.Space, KeyModifiers.None);
        if (disable)
            Click(manager);

        Assert.True(button.IsChecked);
        Assert.True(button.HasPseudoClass(UiPseudoClass.Get("checked")));
        Assert.False(button.HasPseudoClass(UiPseudoClass.Pressed));
        Assert.Equal(0, clicks);
        manager.Close();
    }

    [Fact]
    public void TwoWayPropertyBindingSurvivesActivationAndSynchronizesPeers()
    {
        var (manager, screen, button) = OpenScene();
        var model = new BooleanModel();
        var peer = new ToggleButton();
        button.BindTwoWay(ToggleButton.IsCheckedProperty, model, BooleanModel.ValueProperty);
        peer.BindTwoWay(ToggleButton.IsCheckedProperty, model, BooleanModel.ValueProperty);
        var observed = new List<bool>();
        button.Click += (_, _) => observed.Add(model.Value);

        Click(manager);
        Assert.True(model.Value);
        Assert.True(peer.IsChecked);
        model.Value = false;
        Assert.False(button.IsChecked);
        Assert.False(peer.IsChecked);
        Click(manager);

        Assert.True(button.IsBound(ToggleButton.IsCheckedProperty));
        Assert.True(peer.IsChecked);
        Assert.Equal(new[] { true, true }, observed);
        manager.Close();
    }

    [Fact]
    public void OneWayBindingDoesNotWriteBackAndLaterSourceChangesResynchronize()
    {
        var (manager, screen, button) = OpenScene();
        var model = new BooleanModel();
        button.Bind(ToggleButton.IsCheckedProperty, model, BooleanModel.ValueProperty);

        Click(manager);
        Assert.True(button.IsChecked);
        Assert.False(model.Value);
        Assert.True(button.IsBound(ToggleButton.IsCheckedProperty));
        model.Value = true;
        model.Value = false;

        Assert.False(button.IsChecked);
        Assert.False(button.HasPseudoClass(UiPseudoClass.Get("checked")));
        manager.Close();
    }

    [Fact]
    public void NotifyPropertyChangedBindingWritesBackAndReflectsExternalChanges()
    {
        var (manager, screen, button) = OpenScene();
        var model = new NotifyingBooleanModel();
        button.BindTwoWay(ToggleButton.IsCheckedProperty, model, source => source.Value);
        var clicks = 0;
        button.Click += (_, _) => clicks++;

        Click(manager);
        Assert.True(model.Value);
        model.Value = false;

        Assert.False(button.IsChecked);
        Assert.Equal(1, clicks);
        Assert.True(button.IsBound(ToggleButton.IsCheckedProperty));
        manager.Close();
    }

    [Fact]
    public void ClickObservesStateCorrectedByTheBindingSource()
    {
        var (manager, screen, button) = OpenScene();
        var model = new NotifyingBooleanModel { RejectTrue = true };
        button.BindTwoWay(ToggleButton.IsCheckedProperty, model, source => source.Value);
        var observed = new List<bool>();
        button.Click += (_, _) => observed.Add(button.IsChecked);

        Click(manager);

        Assert.Equal(new[] { false }, observed);
        Assert.False(model.Value);
        Assert.False(button.HasPseudoClass(UiPseudoClass.Get("checked")));
        Assert.True(button.IsBound(ToggleButton.IsCheckedProperty));
        manager.Close();
    }

    [Fact]
    public void PropertyNotificationFailurePreservesStateAndStopsClick()
    {
        var (manager, screen, button) = OpenScene();
        var error = new InvalidOperationException("state notification");
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        using var subscription = button.Subscribe(ToggleButton.IsCheckedProperty, (_, _) => throw error);

        var actual = Assert.Throws<InvalidOperationException>(() => Click(manager));

        Assert.Same(error, actual);
        Assert.True(button.IsChecked);
        Assert.True(button.HasPseudoClass(UiPseudoClass.Get("checked")));
        Assert.Equal(0, clicks);
        manager.Close();
    }

    [Fact]
    public void BindingWriteFailurePreservesTargetAndStopsClick()
    {
        var (manager, screen, button) = OpenScene();
        var error = new InvalidOperationException("source write");
        var model = new NotifyingBooleanModel { WriteError = error };
        button.BindTwoWay(ToggleButton.IsCheckedProperty, model, source => source.Value);
        var clicks = 0;
        button.Click += (_, _) => clicks++;

        var actual = Assert.Throws<InvalidOperationException>(() => Click(manager));

        Assert.Same(error, actual);
        Assert.True(button.IsChecked);
        Assert.False(model.Value);
        Assert.True(button.IsBound(ToggleButton.IsCheckedProperty));
        Assert.Equal(0, clicks);
        manager.Close();
    }

    [Fact]
    public void ClickFailureDoesNotRollbackTheToggle()
    {
        var (manager, screen, button) = OpenScene();
        var error = new InvalidOperationException("click");
        button.Click += (_, _) => throw error;

        var actual = Assert.Throws<InvalidOperationException>(() => Click(manager));

        Assert.Same(error, actual);
        Assert.True(button.IsChecked);
        manager.Close();
    }

    [Fact]
    public void CheckedStylesCombineWithInputStatesAndInheritFocusAndDisabledColors()
    {
        var button = new ToggleButton { IsChecked = true };
        var ordinary = new Button();
        button.UpdateStyles();
        Assert.Equal("ToggleButton:checked", BackgroundSelector(button));

        button.SetHovered(true);
        button.UpdateStyles();
        Assert.Equal("ToggleButton:checked:hover", BackgroundSelector(button));
        button.SetPressed(true);
        button.SetFocused(true);
        ordinary.SetFocused(true);
        button.UpdateStyles();
        ordinary.UpdateStyles();
        Assert.Equal("ToggleButton:checked:pressed", BackgroundSelector(button));
        Assert.Equal(ordinary.BorderBrush, button.BorderBrush);

        button.IsEnabled = false;
        ordinary.IsEnabled = false;
        button.UpdateStyles();
        ordinary.UpdateStyles();
        Assert.Equal("ToggleButton:checked:disabled", BackgroundSelector(button));
        Assert.Equal(ordinary.Foreground, button.Foreground);
        Assert.Equal(ordinary.BorderBrush, button.BorderBrush);
        Assert.NotEqual(ordinary.Background, button.Background);

        var local = new SolidColorBrush(13, 27, 39);
        button.Background = local;
        button.IsChecked = false;
        button.UpdateStyles();
        Assert.Equal(local, button.Background);
        button.ClearValue(Region.BackgroundProperty);
        Assert.Equal(ordinary.Background, button.Background);
    }

    private static (UiManager Manager, GameScreen Screen, ToggleButton Button) OpenScene()
    {
        var root = new Canvas();
        var button = new ToggleButton { Width = 80, Height = 32 };
        root.Children.Add(button);
        var screen = new GameScreen(root);
        var manager = new UiManager();
        manager.Open(screen);
        manager.UpdateFrame(new Size(100, 100), 0);
        return (manager, screen, button);
    }

    private static void Click(UiManager manager, MouseButton button = MouseButton.Left)
    {
        manager.ProcessPointerPressed(new Point(5, 5), button, KeyModifiers.None);
        manager.ProcessPointerReleased(new Point(5, 5), button, KeyModifiers.None);
    }

    private static string? BackgroundSelector(UiNode node) =>
        node.GetStyleValueSources(Region.BackgroundProperty).SingleOrDefault()?.SelectorText;

    private sealed class BooleanModel : ObservableObject
    {
        public static readonly Property<bool> ValueProperty = Property.Register<BooleanModel, bool>(nameof(Value));

        public bool Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }
    }

    private sealed class NotifyingBooleanModel : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _value;

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public bool RejectTrue { get; init; }
        public Exception? WriteError { get; init; }

        public bool Value
        {
            get => _value;
            set
            {
                if (WriteError is { } error)
                    throw error;
                var accepted = value && !RejectTrue;
                if (_value == accepted)
                    return;
                _value = accepted;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Value)));
            }
        }
    }
}

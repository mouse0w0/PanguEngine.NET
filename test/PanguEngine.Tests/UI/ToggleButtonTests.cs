using System.ComponentModel;
using PanguEngine.Client.UI;
using PanguEngine.ComponentModel;
using PanguEngine.Input;
using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Drawing;
using PanguEngine.UI.Styling;
using PropertyChangedEventArgs = System.ComponentModel.PropertyChangedEventArgs;

namespace PanguEngine.Tests.UI;

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
        Assert.False(button.HasPseudoClass(PseudoClass.Checked));
        Assert.Equal("Button", BackgroundSelector(button));
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
            Assert.Equal(args.NewValue, button.HasPseudoClass(PseudoClass.Checked));
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
        var (manager, scene, button) = OpenScene();
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
        var (manager, scene, button) = OpenScene();
        var observed = new List<bool>();
        button.Click += (_, _) => observed.Add(button.IsChecked);
        Assert.True(button.Focus());

        manager.ProcessKeyDown(key, KeyModifiers.None);
        manager.ProcessKeyDown(key, KeyModifiers.None, isRepeat: true);
        Assert.Equal(key == Key.Enter, button.IsChecked);
        Assert.Equal(key == Key.Enter ? 1 : 0, observed.Count);
        if (key == Key.Space)
            Assert.True(button.HasPseudoClass(PseudoClass.Pressed));
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
        var (manager, scene, button) = OpenScene();
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
        var (manager, scene, button) = OpenScene();
        button.IsChecked = true;
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        Assert.True(button.Focus());
        manager.ProcessKeyDown(Key.Space, KeyModifiers.None);

        if (disable)
            button.IsEnabled = false;
        else
            scene.ClearFocus();
        manager.ProcessKeyUp(Key.Space, KeyModifiers.None);
        if (disable)
            Click(manager);

        Assert.True(button.IsChecked);
        Assert.True(button.HasPseudoClass(PseudoClass.Checked));
        Assert.False(button.HasPseudoClass(PseudoClass.Pressed));
        Assert.Equal(0, clicks);
        manager.Close();
    }

    [Fact]
    public void TwoWayPropertyBindingSurvivesActivationAndSynchronizesPeers()
    {
        var (manager, scene, button) = OpenScene();
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
        var (manager, scene, button) = OpenScene();
        var model = new BooleanModel();
        button.Bind(ToggleButton.IsCheckedProperty, model, BooleanModel.ValueProperty);

        Click(manager);
        Assert.True(button.IsChecked);
        Assert.False(model.Value);
        Assert.True(button.IsBound(ToggleButton.IsCheckedProperty));
        model.Value = true;
        model.Value = false;

        Assert.False(button.IsChecked);
        Assert.False(button.HasPseudoClass(PseudoClass.Checked));
        manager.Close();
    }

    [Fact]
    public void NotifyPropertyChangedBindingWritesBackAndReflectsExternalChanges()
    {
        var (manager, scene, button) = OpenScene();
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
    public void ClickObservesTheTargetValueWhenTheSourceRejectsWithoutNotification()
    {
        var (manager, scene, button) = OpenScene();
        var model = new NotifyingBooleanModel { RejectTrue = true };
        button.BindTwoWay(ToggleButton.IsCheckedProperty, model, source => source.Value);
        var observed = new List<bool>();
        button.Click += (_, _) => observed.Add(button.IsChecked);

        Click(manager);

        Assert.Equal(new[] { true }, observed);
        Assert.False(model.Value);
        Assert.True(button.HasPseudoClass(PseudoClass.Checked));
        Assert.True(button.IsBound(ToggleButton.IsCheckedProperty));
        manager.Close();
    }

    [Fact]
    public void PropertyNotificationFailurePreservesStateAndStopsClick()
    {
        var (manager, scene, button) = OpenScene();
        var error = new InvalidOperationException("state notification");
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        using var subscription = button.Subscribe(ToggleButton.IsCheckedProperty, (_, _) => throw error);

        var actual = Assert.Throws<InvalidOperationException>(() => Click(manager));

        Assert.Same(error, actual);
        Assert.True(button.IsChecked);
        Assert.True(button.HasPseudoClass(PseudoClass.Checked));
        Assert.Equal(0, clicks);
        manager.Close();
    }

    [Fact]
    public void BindingWriteFailurePreservesTargetAndStopsClick()
    {
        var (manager, scene, button) = OpenScene();
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
        var (manager, scene, button) = OpenScene();
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
        var (manager, _, button) = OpenScene();
        button.IsChecked = true;
        var ordinary = new Button();
        button.GetStyleRoot().UpdateStyles();
        Assert.Equal("ToggleButton:checked", BackgroundSelector(button));

        manager.ProcessPointerMoved(new Point(5, 5));
        button.GetStyleRoot().UpdateStyles();
        Assert.Equal("ToggleButton:checked:hover", BackgroundSelector(button));
        manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
        ordinary.SetFocused(true);
        button.GetStyleRoot().UpdateStyles();
        ordinary.UpdateStyles();
        Assert.Equal("ToggleButton:checked:pressed", BackgroundSelector(button));
        Assert.Equal(ordinary.BorderBrush, button.BorderBrush);

        button.IsEnabled = false;
        ordinary.IsEnabled = false;
        button.GetStyleRoot().UpdateStyles();
        ordinary.UpdateStyles();
        Assert.Equal("ToggleButton:checked:disabled", BackgroundSelector(button));
        Assert.Equal(ordinary.Color, button.Color);
        Assert.Equal(ordinary.BorderBrush, button.BorderBrush);
        Assert.NotEqual(ordinary.Background, button.Background);

        var local = new SolidColorBrush(13, 27, 39);
        button.Background = local;
        button.IsChecked = false;
        button.GetStyleRoot().UpdateStyles();
        Assert.Equal(local, button.Background);
        button.ClearValue(Region.BackgroundProperty);
        Assert.Equal(ordinary.Background, button.Background);
        manager.Close();
    }

    private static (UIManager Manager, GameUIScene Scene, ToggleButton Button) OpenScene()
    {
        var root = new Canvas();
        var button = new ToggleButton { Width = 80, Height = 32 };
        root.Children.Add(button);
        var scene = new GameUIScene(root);
        var manager = new UIManager();
        manager.Open(scene);
        manager.UpdateFrame(new Size(100, 100), 0);
        return (manager, scene, button);
    }

    private static void Click(UIManager manager, MouseButton button = MouseButton.Left)
    {
        manager.ProcessPointerPressed(new Point(5, 5), button, KeyModifiers.None);
        manager.ProcessPointerReleased(new Point(5, 5), button, KeyModifiers.None);
    }

    private static string? BackgroundSelector(UIElement node) =>
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

    private sealed class NotifyingBooleanModel : INotifyPropertyChanged
    {
        private bool _value;

        public event PropertyChangedEventHandler? PropertyChanged;

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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
    }
}

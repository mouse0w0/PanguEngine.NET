using PanguEngine.Client.UI;
using PanguEngine.Input;
using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Styling;

namespace PanguEngine.Tests.UI;

[Collection(TextServicesCollection.Name)]
public sealed class ContentActivationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void NestedSliderConsumesItsClickWithoutActivatingTheOuterControl(int kind)
    {
        var (control, activations) = CreateControl(kind);
        var slider = new Slider<int>(new NumericSliderValueModel<int> { Minimum = 0, Maximum = 100 })
        {
            Width = 90,
            Height = 24,
            Value = 50
        };
        var innerClicks = 0;
        var ancestorClicks = 0;
        slider.PointerClicked += (_, _) => innerClicks++;
        var root = new Canvas();
        root.PointerClicked += (_, _) => ancestorClicks++;
        control.Content = slider;
        var (manager, _) = Open(control, root);
        var point = slider.LocalToScene(new Point(slider.LayoutBounds.Width / 2, slider.LayoutBounds.Height / 2));

        manager.ProcessPointerPressed(point, MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerReleased(point, MouseButton.Left, KeyModifiers.None);

        Assert.Equal(1, innerClicks);
        Assert.Equal(0, ancestorClicks);
        Assert.Equal(0, activations());
        manager.ProcessPointerPressed(point, MouseButton.Right, KeyModifiers.None);
        manager.ProcessPointerReleased(point, MouseButton.Right, KeyModifiers.None);
        Assert.Equal(2, innerClicks);
        Assert.Equal(1, ancestorClicks);
        Assert.Equal(0, activations());
        manager.Close();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void FocusedEditorKeysDoNotCreateAnOuterActivationPair(int kind)
    {
        using var context = new TextTestContext();
        var (control, activations) = CreateControl(kind);
        var editor = new TextBox { Width = 90, Height = 24 };
        control.Content = editor;
        var (manager, _) = Open(control);
        Assert.True(editor.Focus());

        manager.ProcessKeyDown(Key.Space, KeyModifiers.None);
        Assert.False(control.HasPseudoClass(PseudoClass.Pressed));
        Assert.True(control.Focus());
        manager.ProcessKeyUp(Key.Space, KeyModifiers.None);
        Assert.Equal(0, activations());
        Assert.True(editor.Focus());
        manager.ProcessKeyDown(Key.Enter, KeyModifiers.None);
        manager.ProcessKeyUp(Key.Enter, KeyModifiers.None);

        Assert.Equal(0, activations());
        manager.Close();
    }

    [Theory]
    [InlineData(0, Key.Enter)]
    [InlineData(0, Key.Space)]
    [InlineData(1, Key.Space)]
    [InlineData(2, Key.Space)]
    public void CancellingFocusabilityInAKeyCallbackSuppressesActivation(int kind, Key key)
    {
        var (control, activations) = CreateControl(kind);
        if (key == Key.Enter)
            control.KeyDown += (_, _) => control.Focusable = false;
        else
            control.KeyUp += (_, _) => control.Focusable = false;
        var (manager, _) = Open(control);
        Assert.True(control.Focus());

        manager.ProcessKeyDown(key, KeyModifiers.None);
        manager.ProcessKeyUp(key, KeyModifiers.None);

        Assert.Equal(0, activations());
        manager.Close();
    }

    [Fact]
    public void MovingTheOriginalPressTargetOutCancelsItsOuterActivationEvenIfItReturns()
    {
        var child = new Panel { Width = 50, Height = 20 };
        var content = new Panel { Width = 50, Height = 20 };
        content.Children.Add(child);
        var button = new Button { Content = content };
        var root = new Canvas();
        var (manager, scene) = Open(button, root);
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        var point = child.LocalToScene(new Point(5, 5));
        manager.ProcessPointerPressed(point, MouseButton.Left, KeyModifiers.None);
        Assert.True(button.HasPseudoClass(PseudoClass.Pressed));

        root.Children.Add(child);

        Assert.False(button.HasPseudoClass(PseudoClass.Pressed));
        content.Children.Add(child);
        manager.UpdateFrame(new Size(260, 100), 0);
        manager.ProcessPointerReleased(point, MouseButton.Left, KeyModifiers.None);
        Assert.Equal(0, clicks);
        Assert.Same(scene, button.Scene);
        manager.Close();
    }

    [Fact]
    public void ContentClickHandlerCanCancelTheOuterActivation()
    {
        var child = new Panel { Width = 50, Height = 20 };
        var button = new Button { Content = child };
        child.PointerClicked += (_, args) => args.Handled = true;
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        var (manager, _) = Open(button);
        var point = child.LocalToScene(new Point(5, 5));

        manager.ProcessPointerPressed(point, MouseButton.Left, KeyModifiers.None);
        manager.ProcessPointerReleased(point, MouseButton.Left, KeyModifiers.None);

        Assert.Equal(0, clicks);
        Assert.False(button.HasPseudoClass(PseudoClass.Pressed));
        manager.Close();
    }

    [Theory]
    [InlineData(Key.Enter)]
    [InlineData(Key.Space)]
    public void AButtonKeyHandlerCanConsumeItsActivation(Key key)
    {
        var button = new Button();
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        if (key == Key.Enter)
            button.KeyDown += (_, args) => args.Handled = true;
        else
            button.KeyUp += (_, args) => args.Handled = true;
        var (manager, _) = Open(button);
        Assert.True(button.Focus());

        manager.ProcessKeyDown(key, KeyModifiers.None);
        manager.ProcessKeyUp(key, KeyModifiers.None);

        Assert.Equal(0, clicks);
        Assert.False(button.HasPseudoClass(PseudoClass.Pressed));
        manager.Close();
    }

    private static (ContentControl Control, Func<int> Activations) CreateControl(int kind)
    {
        var count = 0;
        ContentControl control;
        switch (kind)
        {
            case 0:
                var button = new Button();
                button.Click += (_, _) => count++;
                control = button;
                break;
            case 1:
                var box = new CheckBox();
                box.Click += (_, _) => count++;
                control = box;
                break;
            default:
                var radio = new RadioButton<string> { Group = new RadioGroup<string>(), Value = "option" };
                radio.Subscribe(RadioButton.IsCheckedProperty, (_, change) => count += change.NewValue ? 1 : 0);
                control = radio;
                break;
        }
        return (control, () => count);
    }

    private static (UIManager Manager, GameUIScene Scene) Open(ContentControl control, Canvas? root = null)
    {
        root ??= new Canvas();
        root.Children.Add(control);
        control.Width = 200;
        control.Height = 60;
        var manager = new UIManager();
        var scene = new GameUIScene(root);
        manager.Open(scene);
        manager.UpdateFrame(new Size(260, 100), 0);
        return (manager, scene);
    }
}

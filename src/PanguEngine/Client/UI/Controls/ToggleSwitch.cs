using PanguEngine.Client.UI.Input;
using PanguEngine.Client.UI.Styling;
using PanguEngine.ComponentModel;
using PanguEngine.Input;

namespace PanguEngine.Client.UI.Controls;

/// <summary>
/// Provides a focusable switch that represents an on or off state.
/// </summary>
public sealed class ToggleSwitch : Control
{
    private static readonly UiPseudoClass CheckedPseudoClass = UiPseudoClass.Get("checked");

    /// <summary>
    /// Identifies the <see cref="IsOn"/> property.
    /// </summary>
    public static readonly DirectProperty<ToggleSwitch, bool> IsOnProperty =
        Property.RegisterDirect<ToggleSwitch, bool>(
            nameof(IsOn),
            static control => control.IsOn,
            static (control, value) => control.IsOn = value);

    private readonly Panel _thumb;
    private bool _enterKeyDown;
    private bool _spaceKeyDown;

    static ToggleSwitch()
    {
        UiCssRegistry.RegisterElement<ToggleSwitch>("ToggleSwitch");
    }

    /// <summary>
    /// Initializes a switch with keyboard focus enabled.
    /// </summary>
    public ToggleSwitch()
    {
        Focusable = true;
        ClipToBounds = true;
        _thumb = new Panel { IsHitTestVisible = false };
        _thumb.Classes.Add("toggle-switch-thumb");
        Children.Add(_thumb);
    }

    /// <summary>
    /// Gets or sets whether the switch is on.
    /// </summary>
    public bool IsOn
    {
        get;
        set => SetField(IsOnProperty, ref field, value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(PropertyChangedEventArgs eventArgs)
    {
        if (ReferenceEquals(eventArgs.Property, IsOnProperty))
        {
            SetPseudoClass(CheckedPseudoClass, IsOn);
            InvalidateArrange();
        }

        base.OnPropertyChanged(eventArgs);
    }

    /// <inheritdoc />
    protected override Size MeasureContent(Size availableSize)
    {
        _thumb.Measure(availableSize);
        return _thumb.DesiredSize;
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Rect contentBounds)
    {
        var width = Math.Min(_thumb.DesiredSize.Width, contentBounds.Width);
        var height = Math.Min(_thumb.DesiredSize.Height, contentBounds.Height);
        _thumb.Arrange(new Rect(
            IsOn ? contentBounds.X + contentBounds.Width - width : contentBounds.X,
            contentBounds.Y + (contentBounds.Height - height) / 2,
            width,
            height));
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(UiPointerButtonEventArgs eventArgs)
    {
        base.OnPointerPressed(eventArgs);
        if (eventArgs.Button == MouseButton.Left)
            eventArgs.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(UiPointerButtonEventArgs eventArgs)
    {
        base.OnPointerReleased(eventArgs);
        if (eventArgs.Button == MouseButton.Left)
            eventArgs.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerClicked(UiPointerButtonEventArgs eventArgs)
    {
        var screen = Screen;
        base.OnPointerClicked(eventArgs);
        if (eventArgs.Handled || eventArgs.Button != MouseButton.Left)
            return;

        eventArgs.Handled = true;
        if (CanReceivePointerInput(screen))
            IsOn = !IsOn;
    }

    /// <inheritdoc />
    protected override void OnKeyDown(UiKeyEventArgs eventArgs)
    {
        base.OnKeyDown(eventArgs);
        if (eventArgs.Handled || !IsEnabled || !IsFocused)
            return;

        switch (eventArgs.Key)
        {
            case Key.Enter:
                eventArgs.Handled = true;
                if (!eventArgs.IsRepeat && !_enterKeyDown)
                {
                    _enterKeyDown = true;
                    IsOn = !IsOn;
                }
                break;
            case Key.Space:
                eventArgs.Handled = true;
                if (!eventArgs.IsRepeat)
                    SetSpaceKeyDown(true);
                break;
        }
    }

    /// <inheritdoc />
    protected override void OnKeyUp(UiKeyEventArgs eventArgs)
    {
        var activate = eventArgs.Key == Key.Space && _spaceKeyDown;
        if (eventArgs.Key == Key.Space)
            SetSpaceKeyDown(false);
        else if (eventArgs.Key == Key.Enter)
            _enterKeyDown = false;

        base.OnKeyUp(eventArgs);
        if (eventArgs.Handled || !IsEnabled || !IsFocused ||
            eventArgs.Key is not (Key.Enter or Key.Space))
        {
            return;
        }

        eventArgs.Handled = true;
        if (activate)
            IsOn = !IsOn;
    }

    /// <inheritdoc />
    protected override void OnLostFocus(UiFocusChangedEventArgs eventArgs)
    {
        _enterKeyDown = false;
        SetSpaceKeyDown(false);
        base.OnLostFocus(eventArgs);
    }

    /// <inheritdoc />
    protected override bool IsPressedPseudoClassActive => IsPressed || _spaceKeyDown;

    private bool CanReceivePointerInput(UiScreen? screen)
    {
        if (screen is null || !ReferenceEquals(Screen, screen) || !screen.IsOpen())
            return false;

        for (UiNode? node = this; node is not null; node = node.Parent)
        {
            if (!node.IsEnabled || node.Visibility != Visibility.Visible)
                return false;
        }

        return true;
    }

    private void SetSpaceKeyDown(bool value)
    {
        if (_spaceKeyDown == value)
            return;

        _spaceKeyDown = value;
        RefreshPressedPseudoClass();
    }
}

using PanguEngine.Input;
using PanguEngine.UI.Input;
using PanguEngine.UI.Styling;

namespace PanguEngine.UI.Controls;

/// <summary>
/// Provides a clickable control with data or element content.
/// </summary>
public class Button : ContentControl
{
    static Button()
    {
        CssRegistry.RegisterElement<Button>("Button");
    }

    private bool _enterKeyDown;
    private bool _spaceKeyDown;

    /// <summary>
    /// Initializes a button with keyboard focus enabled.
    /// </summary>
    public Button()
    {
        Focusable = true;
    }

    /// <summary>
    /// Occurs when pointer or keyboard input activates this button.
    /// </summary>
    public event EventHandler? Click;

    /// <summary>
    /// Raises the <see cref="Click"/> event when this button is activated.
    /// </summary>
    protected virtual void OnClick() => Click?.Invoke(this, EventArgs.Empty);

    /// <inheritdoc />
    protected override Size MeasureContent(Size availableSize)
    {
        ContentRoot?.Measure(availableSize);
        return ContentRoot?.DesiredSize ?? Size.Zero;
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Rect contentBounds)
    {
        if (ContentRoot is not { } root)
            return;
        var desired = root.DesiredSize;
        root.Arrange(new Rect(
            contentBounds.X + (contentBounds.Width - desired.Width) / 2,
            contentBounds.Y + (contentBounds.Height - desired.Height) / 2,
            desired));
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerButtonEventArgs eventArgs)
    {
        var scene = Scene;
        base.OnPointerPressed(eventArgs);
        if (!eventArgs.Handled && eventArgs.Button == MouseButton.Left &&
            ReferenceEquals(Scene, scene))
            eventArgs.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerButtonEventArgs eventArgs)
    {
        var scene = Scene;
        base.OnPointerReleased(eventArgs);
        if (!eventArgs.Handled && eventArgs.Button == MouseButton.Left &&
            ReferenceEquals(Scene, scene))
            eventArgs.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerClicked(PointerButtonEventArgs eventArgs)
    {
        var scene = Scene;
        base.OnPointerClicked(eventArgs);
        if (eventArgs.Handled || eventArgs.Button != MouseButton.Left ||
            !ReferenceEquals(Scene, scene))
            return;
        eventArgs.Handled = true;
        OnClick();
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs eventArgs)
    {
        if (!ReferenceEquals(eventArgs.Source, this) || !IsFocused || !Focusable)
        {
            base.OnKeyDown(eventArgs);
            return;
        }
        switch (eventArgs.Key)
        {
            case Key.Enter:
            {
                var activate = !_enterKeyDown && !eventArgs.IsRepeat;
                _enterKeyDown = true;
                base.OnKeyDown(eventArgs);
                if (eventArgs.Handled || !IsFocused || !Focusable)
                    return;
                eventArgs.Handled = true;
                if (activate && _enterKeyDown)
                    OnClick();
                return;
            }
            case Key.Space:
                if (!eventArgs.IsRepeat)
                    SetSpaceKeyDown(true);
                base.OnKeyDown(eventArgs);
                if (eventArgs.Handled || !IsFocused || !Focusable)
                {
                    SetSpaceKeyDown(false);
                    return;
                }
                eventArgs.Handled = true;
                return;
            default:
                base.OnKeyDown(eventArgs);
                return;
        }
    }

    /// <inheritdoc />
    protected override void OnKeyUp(KeyEventArgs eventArgs)
    {
        if (!ReferenceEquals(eventArgs.Source, this) || !IsFocused || !Focusable)
        {
            base.OnKeyUp(eventArgs);
            return;
        }
        switch (eventArgs.Key)
        {
            case Key.Enter:
                _enterKeyDown = false;
                base.OnKeyUp(eventArgs);
                if (eventArgs.Handled || !IsFocused || !Focusable)
                    return;
                eventArgs.Handled = true;
                return;
            case Key.Space:
            {
                var activate = false;
                try
                {
                    base.OnKeyUp(eventArgs);
                    if (!eventArgs.Handled && IsFocused && Focusable)
                    {
                        eventArgs.Handled = true;
                        activate = _spaceKeyDown;
                    }
                }
                finally
                {
                    SetSpaceKeyDown(false);
                }
                if (activate && IsFocused && Focusable)
                    OnClick();
                return;
            }
            default:
                base.OnKeyUp(eventArgs);
                return;
        }
    }

    /// <inheritdoc />
    protected override void OnLostFocus(FocusChangedEventArgs eventArgs)
    {
        _enterKeyDown = false;
        SetSpaceKeyDown(false);
        base.OnLostFocus(eventArgs);
    }

    /// <inheritdoc />
    protected override bool IsPressedPseudoClassActive => base.IsPressedPseudoClassActive || _spaceKeyDown;

    private void SetSpaceKeyDown(bool value)
    {
        if (_spaceKeyDown == value)
            return;
        _spaceKeyDown = value;
        RefreshPressedPseudoClass();
    }
}

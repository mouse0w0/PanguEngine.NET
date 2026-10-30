using PanguEngine.ComponentModel;
using PanguEngine.Input;
using PanguEngine.UI.Input;
using PanguEngine.UI.Styling;

namespace PanguEngine.UI.Controls;

/// <summary>
/// Provides a checkable control with optional text or element content and an indeterminate state.
/// </summary>
public sealed class CheckBox : ContentControl
{
    /// <summary>Identifies the <see cref="IsChecked"/> property.</summary>
    public static readonly Property<bool?> IsCheckedProperty =
        Property.Register<CheckBox, bool?>(nameof(IsChecked), false,
            onChanged: static (box, _, _) => box.SynchronizeCheckState());

    /// <summary>Identifies the <see cref="IsThreeState"/> property.</summary>
    public static readonly Property<bool> IsThreeStateProperty =
        Property.Register<CheckBox, bool>(nameof(IsThreeState));

    /// <summary>Identifies the <see cref="Spacing"/> property.</summary>
    public static readonly Property<double> SpacingProperty =
        Property.Register<CheckBox, double>(nameof(Spacing), 8,
            onChanged: static (box, _, _) => box.InvalidateMeasure(),
            validate: IsFiniteNonNegative,
            validationMessage: "Spacing must be a finite non-negative value.");

    private readonly Panel _box;
    private readonly Path _mark;
    private bool _spaceKeyDown;

    static CheckBox()
    {
        CssRegistry.RegisterElement<CheckBox>("CheckBox");
        CssRegistry.RegisterProperty<CheckBox, double>("spacing", SpacingProperty, CssValueConverters.ParseLength);
    }

    /// <summary>Initializes a focusable check box with an empty label.</summary>
    public CheckBox()
    {
        Focusable = true;
        _box = new Panel { IsHitTestVisible = false };
        _box.Classes.Add("checkbox-box");
        _mark = new Path { IsHitTestVisible = false };
        _mark.Classes.Add("checkbox-mark");
        _box.Children.Add(_mark);
        ContentSlot.DefaultTextClass = "checkbox-text";
        Children.Insert(0, _box);
        SynchronizeCheckState();
    }

    /// <summary>Gets or sets the checked state, or null for an indeterminate state.</summary>
    public bool? IsChecked
    {
        get => GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    /// <summary>Gets or sets whether user activation cycles through all three states.</summary>
    /// <remarks>Programmatic indeterminate values are accepted regardless of this setting.</remarks>
    public bool IsThreeState
    {
        get => GetValue(IsThreeStateProperty);
        set => SetValue(IsThreeStateProperty, value);
    }

    /// <summary>Gets or sets the logical-pixel gap between the box and a label that participates in layout.</summary>
    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <summary>Occurs after pointer or keyboard activation updates the checked state.</summary>
    public event EventHandler? Click;

    /// <inheritdoc />
    protected override Size MeasureContent(Size availableSize)
    {
        _box.Measure(availableSize);
        var spacing = GetLayoutSpacing();
        var remainingWidth = double.IsPositiveInfinity(availableSize.Width)
            ? double.PositiveInfinity
            : Math.Max(0, availableSize.Width - _box.DesiredSize.Width - spacing);
        ContentRoot?.Measure(new Size(remainingWidth, availableSize.Height));
        var contentSize = ContentRoot?.DesiredSize ?? Size.Zero;
        return new Size(
            _box.DesiredSize.Width + spacing + contentSize.Width,
            Math.Max(_box.DesiredSize.Height, contentSize.Height));
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Rect contentBounds)
    {
        var boxSize = _box.DesiredSize;
        _box.Arrange(new Rect(contentBounds.X,
            contentBounds.Y + (contentBounds.Height - boxSize.Height) / 2, boxSize));
        var x = contentBounds.X + boxSize.Width + GetLayoutSpacing();
        var contentHeight = ContentRoot?.DesiredSize.Height ?? 0;
        ContentRoot?.Arrange(new Rect(x, contentBounds.Y + (contentBounds.Height - contentHeight) / 2,
            Math.Max(0, contentBounds.X + contentBounds.Width - x), contentHeight));
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
        if (eventArgs.Handled || eventArgs.Button != MouseButton.Left || !ReferenceEquals(Scene, scene))
            return;
        eventArgs.Handled = true;
        Activate();
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs eventArgs)
    {
        if (eventArgs.Key != Key.Space || !ReferenceEquals(eventArgs.Source, this) || !IsFocused ||
            !Focusable)
        {
            base.OnKeyDown(eventArgs);
            return;
        }

        if (!eventArgs.IsRepeat)
            SetSpaceKeyDown(IsEnabled && IsFocused);
        base.OnKeyDown(eventArgs);
        if (eventArgs.Handled || !IsFocused || !Focusable)
        {
            SetSpaceKeyDown(false);
            return;
        }
        eventArgs.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnKeyUp(KeyEventArgs eventArgs)
    {
        if (eventArgs.Key != Key.Space || !ReferenceEquals(eventArgs.Source, this) || !IsFocused ||
            !Focusable)
        {
            base.OnKeyUp(eventArgs);
            return;
        }

        var activate = false;
        var scene = Scene;
        try
        {
            base.OnKeyUp(eventArgs);
            if (!eventArgs.Handled && IsFocused && Focusable && ReferenceEquals(Scene, scene))
            {
                eventArgs.Handled = true;
                activate = _spaceKeyDown;
            }
        }
        finally
        {
            SetSpaceKeyDown(false);
        }
        if (activate && IsFocused && Focusable && ReferenceEquals(Scene, scene))
            Activate();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(FocusChangedEventArgs eventArgs)
    {
        SetSpaceKeyDown(false);
        base.OnLostFocus(eventArgs);
    }

    /// <inheritdoc />
    protected override bool IsPressedPseudoClassActive => base.IsPressedPseudoClassActive || _spaceKeyDown;

    private void SynchronizeCheckState()
    {
        var state = IsChecked;
        SetPseudoClass(PseudoClass.Checked, state == true);
        SetPseudoClass(PseudoClass.Indeterminate, state is null);
    }

    private double GetLayoutSpacing()
    {
        if (ContentRoot is not { Visibility: not Visibility.Collapsed })
            return 0;
        var spacing = Spacing;
        var scene = Scene;
        return scene?.UseLayoutRounding ?? true
            ? LayoutHelper.RoundLayoutValue(spacing, scene?.Scale ?? 1)
            : spacing;
    }

    private void SetSpaceKeyDown(bool value)
    {
        if (_spaceKeyDown == value)
            return;
        _spaceKeyDown = value;
        RefreshPressedPseudoClass();
    }

    private void Activate()
    {
        IsChecked = IsChecked switch
        {
            false => true,
            true when IsThreeState => null,
            _ => false
        };
        Click?.Invoke(this, EventArgs.Empty);
    }
}

using PanguEngine.ComponentModel;
using PanguEngine.Input;
using PanguEngine.UI.Input;
using PanguEngine.UI.Styling;

namespace PanguEngine.UI.Controls;

/// <summary>Provides content, presentation, and input behavior shared by strongly typed radio buttons.</summary>
public abstract class RadioButton : ContentControl
{
    /// <summary>Identifies the <see cref="IsChecked"/> property.</summary>
    public static readonly DirectProperty<RadioButton, bool> IsCheckedProperty =
        Property.RegisterDirect<RadioButton, bool>(nameof(IsChecked),
            static button => button.IsChecked, static (button, value) => button.IsChecked = value);

    /// <summary>Identifies the <see cref="Spacing"/> property.</summary>
    public static readonly Property<double> SpacingProperty =
        Property.Register<RadioButton, double>(nameof(Spacing), 8d,
            onChanged: static (button, _, _) => button.InvalidateMeasure(),
            validate: IsFiniteNonNegative,
            validationMessage: "Spacing must be a finite non-negative value.");

    static RadioButton()
    {
        CssRegistry.RegisterElement<RadioButton>("RadioButton");
        CssRegistry.RegisterProperty<RadioButton, double>("spacing", SpacingProperty, CssValueConverters.ParseLength);
    }

    private readonly RadioButtonIndicator _indicator;
    private bool _isChecked;
    private bool _enterKeyDown;
    private bool _spaceKeyDown;

    /// <summary>Initializes a focusable radio button with no content.</summary>
    private protected RadioButton()
    {
        _indicator = new RadioButtonIndicator();
        ContentSlot.DefaultTextClass = "radio-button-text";
        Children.Insert(0, _indicator);
        Focusable = true;
        ClipToBounds = true;
    }

    /// <summary>Gets or sets whether this button is the selected member of its group.</summary>
    /// <remarks>
    /// Setting true selects this button when its group and value are configured. Setting false
    /// clears the group only when this button is selected. Unconfigured requests have no effect.
    /// </remarks>
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            VerifyMutationAccess();
            SetChecked(value);
        }
    }

    /// <summary>Gets or sets the logical-pixel gap between the indicator and presented content that participates in layout.</summary>
    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureContent(Size availableSize)
    {
        var gap = GetLayoutSpacing();
        _indicator.Measure(availableSize);
        var width = double.IsPositiveInfinity(availableSize.Width)
            ? double.PositiveInfinity
            : Math.Max(0, availableSize.Width - _indicator.DesiredSize.Width - gap);
        ContentRoot?.Measure(new Size(width, availableSize.Height));
        var contentSize = ContentRoot?.DesiredSize ?? Size.Zero;
        return new Size(_indicator.DesiredSize.Width + gap + contentSize.Width,
            Math.Max(_indicator.DesiredSize.Height, contentSize.Height));
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Rect contentBounds)
    {
        var desired = _indicator.DesiredSize;
        var indicatorWidth = Math.Min(desired.Width, contentBounds.Width);
        var indicatorHeight = Math.Min(desired.Height, contentBounds.Height);
        _indicator.Arrange(new Rect(contentBounds.X,
            contentBounds.Y + (contentBounds.Height - indicatorHeight) / 2,
            indicatorWidth, indicatorHeight));
        var gap = GetLayoutSpacing();
        var offset = Math.Min(contentBounds.Width, indicatorWidth + gap);
        ContentRoot?.Arrange(new Rect(contentBounds.X + offset, contentBounds.Y,
            Math.Max(0, contentBounds.Width - offset), contentBounds.Height));
    }

    /// <inheritdoc />
    protected override bool IsPressedPseudoClassActive => base.IsPressedPseudoClassActive || _spaceKeyDown;

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
        SelectFromInput();
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs eventArgs)
    {
        if (!ReferenceEquals(eventArgs.Source, this) || !IsFocused)
        {
            base.OnKeyDown(eventArgs);
            return;
        }
        var eligible = IsFocused && Focusable;
        var activateEnter = eligible && !eventArgs.IsRepeat && !_enterKeyDown;
        if (eligible && !eventArgs.IsRepeat)
        {
            if (eventArgs.Key == Key.Enter)
                _enterKeyDown = true;
            else if (eventArgs.Key == Key.Space)
                SetSpaceKeyDown(true);
        }

        base.OnKeyDown(eventArgs);
        if (eventArgs.Handled || !IsFocused || !Focusable)
        {
            if (eventArgs.Key == Key.Space)
                SetSpaceKeyDown(false);
            return;
        }

        switch (eventArgs.Key)
        {
            case Key.Enter:
                eventArgs.Handled = true;
                if (activateEnter && _enterKeyDown)
                    SelectFromInput();
                break;
            case Key.Space:
                eventArgs.Handled = true;
                break;
            case Key.Left or Key.Up when eventArgs.Modifiers == KeyModifiers.None:
                eventArgs.Handled = Navigate(-1);
                break;
            case Key.Right or Key.Down when eventArgs.Modifiers == KeyModifiers.None:
                eventArgs.Handled = Navigate(1);
                break;
        }
    }

    /// <inheritdoc />
    protected override void OnKeyUp(KeyEventArgs eventArgs)
    {
        if (!ReferenceEquals(eventArgs.Source, this) || !IsFocused)
        {
            base.OnKeyUp(eventArgs);
            return;
        }
        if (eventArgs.Key == Key.Enter)
        {
            _enterKeyDown = false;
            base.OnKeyUp(eventArgs);
            if (!eventArgs.Handled && IsFocused && Focusable)
                eventArgs.Handled = true;
            return;
        }
        if (eventArgs.Key != Key.Space)
        {
            base.OnKeyUp(eventArgs);
            return;
        }
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
            SelectFromInput();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(FocusChangedEventArgs eventArgs)
    {
        _enterKeyDown = false;
        SetSpaceKeyDown(false);
        base.OnLostFocus(eventArgs);
    }

    internal void VerifyGroupAccess() => VerifyMutationAccess();
    internal void CommitChecked(bool value) => _isChecked = value;
    internal void RefreshCheckedPseudoClass() => SetPseudoClass(PseudoClass.Checked, IsChecked);

    internal void PublishCheckedChanged(bool oldValue, bool newValue) =>
        RaisePropertyChanged(IsCheckedProperty, oldValue, newValue);

    /// <summary>Requests selection or cancellation for this button.</summary>
    private protected abstract void SetChecked(bool value);

    /// <summary>Requests selection of this button from user input.</summary>
    private protected abstract void SelectFromInput();

    /// <summary>Requests focus and selection movement within this button's group.</summary>
    private protected abstract bool Navigate(int direction);

    private double GetLayoutSpacing()
    {
        if (ContentRoot is not { Visibility: not Visibility.Collapsed })
            return 0;
        var scene = Scene;
        return scene?.UseLayoutRounding ?? true
            ? LayoutHelper.RoundLayoutValue(Spacing, scene?.Scale ?? 1)
            : Spacing;
    }

    private void SetSpaceKeyDown(bool value)
    {
        if (_spaceKeyDown == value)
            return;
        _spaceKeyDown = value;
        RefreshPressedPseudoClass();
    }
}

/// <summary>Displays content and a single-choice indicator for a business value.</summary>
/// <typeparam name="T">A reference type or nullable value type whose null value means an unconfigured option.</typeparam>
public sealed class RadioButton<T> : RadioButton
{
    /// <summary>Identifies the <see cref="Group"/> property.</summary>
    public static readonly DirectProperty<RadioButton<T>, RadioGroup<T>?> GroupProperty =
        Property.RegisterDirect<RadioButton<T>, RadioGroup<T>?>(nameof(Group),
            static button => button.Group,
            static (button, value) => button.Group = value,
            onChanged: static (button, oldGroup, newGroup) =>
                RadioGroup<T>.SynchronizeMembership(button, oldGroup, newGroup));

    /// <summary>Identifies the <see cref="Value"/> property.</summary>
    public static readonly DirectProperty<RadioButton<T>, T?> ValueProperty =
        Property.RegisterDirect<RadioButton<T>, T?>(nameof(Value),
            static button => button.Value, static (button, value) => button.Value = value,
            onChanged: static (button, _, value) => button._group?.SynchronizeMemberValue(button, value),
            comparer: EqualityComparer<T?>.Default);

    private RadioGroup<T>? _group;
    private T? _value;

    /// <summary>Initializes a radio button with no group, value, or content.</summary>
    /// <exception cref="NotSupportedException">Thrown when T is a non-nullable value type.</exception>
    public RadioButton() => RadioValueType<T>.VerifySupported();

    /// <summary>Gets or sets the explicit selection group, independently of layout ownership.</summary>
    public RadioGroup<T>? Group
    {
        get => _group;
        set
        {
            VerifyMutationAccess();
            if (GroupProperty.ValuesEqual(_group, value))
                return;
            var oldGroup = _group;
            oldGroup?.BeginMutation();
            try
            {
                value?.BeginMutation();
                try
                {
                    SetField(GroupProperty, ref _group, value);
                }
                finally
                {
                    value?.EndMutation();
                }
            }
            finally
            {
                oldGroup?.EndMutation();
            }
        }
    }

    /// <summary>Gets or sets the business value, or null for an option that cannot be selected.</summary>
    public T? Value
    {
        get => _value;
        set
        {
            VerifyMutationAccess();
            if (ValueProperty.ValuesEqual(_value, value))
                return;
            var group = _group;
            group?.BeginMutation();
            try
            {
                SetField(ValueProperty, ref _value, value);
            }
            finally
            {
                group?.EndMutation();
            }
        }
    }

    /// <inheritdoc />
    private protected override void SetChecked(bool value)
    {
        if (_group is not { } group)
            return;
        if (value)
            group.Select(this);
        else if (IsChecked)
            group.Clear();
    }

    /// <inheritdoc />
    private protected override void SelectFromInput()
    {
        if (_group is { } group && _value is not null)
            group.Select(this);
    }

    /// <inheritdoc />
    private protected override bool Navigate(int direction)
    {
        var group = _group;
        if (group is null || _value is null)
            return false;
        var scene = Scene;
        var members = group.GetMembersSnapshot();
        var current = Array.IndexOf(members, this);
        for (var offset = 1; offset < members.Length; offset++)
        {
            var index = (current + direction * offset + members.Length) % members.Length;
            var candidate = members[index];
            if (!ReferenceEquals(candidate.Group, group) || candidate.Value is null ||
                !ReferenceEquals(candidate.Scene, scene) || !candidate.Focus())
                continue;
            if (ReferenceEquals(_group, group) && ReferenceEquals(candidate.Group, group) &&
                ReferenceEquals(candidate.Scene, scene) && candidate.IsFocused && candidate.Focusable &&
                candidate.Value is not null)
                group.Select(candidate);
            return true;
        }

        return true;
    }
}

internal static class RadioValueType<T>
{
    internal static void VerifySupported()
    {
        if (default(T) is not null)
            throw new NotSupportedException("Radio values must use a reference type or a nullable value type.");
    }
}

internal sealed class RadioButtonIndicator : Control
{
    private readonly Ellipse _ring = new() { IsHitTestVisible = false };
    private readonly Ellipse _dot = new() { IsHitTestVisible = false };

    internal RadioButtonIndicator()
    {
        IsHitTestVisible = false;
        Classes.Add("radio-button-indicator");
        _ring.Classes.Add("radio-button-ring");
        _dot.Classes.Add("radio-button-dot");
        Children.Add(_ring);
        Children.Add(_dot);
    }

    protected override Size MeasureContent(Size availableSize)
    {
        _ring.Measure(availableSize);
        _dot.Measure(availableSize);
        return new Size(Math.Max(_ring.DesiredSize.Width, _dot.DesiredSize.Width),
            Math.Max(_ring.DesiredSize.Height, _dot.DesiredSize.Height));
    }

    protected override void ArrangeContent(Rect contentBounds)
    {
        _ring.Arrange(contentBounds);
        var size = _dot.DesiredSize;
        _dot.Arrange(new Rect(contentBounds.X + (contentBounds.Width - size.Width) / 2,
            contentBounds.Y + (contentBounds.Height - size.Height) / 2, size));
    }
}

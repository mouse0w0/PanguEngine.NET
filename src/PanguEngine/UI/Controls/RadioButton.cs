using PanguEngine.ComponentModel;
using PanguEngine.Graphics.Text;
using PanguEngine.Input;
using PanguEngine.UI.Input;
using PanguEngine.UI.Styling;

namespace PanguEngine.UI.Controls;

/// <summary>Provides content, presentation, and input behavior shared by strongly typed radio buttons.</summary>
public abstract class RadioButton : Control
{
    /// <summary>Identifies the <see cref="Content"/> property.</summary>
    public static readonly DirectProperty<RadioButton, object?> ContentProperty =
        Property.RegisterDirect<RadioButton, object?>(nameof(Content),
            static button => button.Content, static (button, value) => button.Content = value,
            onChanged: static (button, _, value) => button.SynchronizeContent(value));

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
    private readonly RadioButtonContentHost _contentHost;
    private object? _content;
    private Node? _contentNode;
    private bool _isChecked;
    private bool _changingContent;
    private bool _enterKeyDown;
    private bool _spaceKeyDown;
    private int _focusVersion;

    /// <summary>Initializes a focusable radio button with no content.</summary>
    private protected RadioButton()
    {
        _indicator = new RadioButtonIndicator();
        _contentHost = new RadioButtonContentHost(NotifyContentDetached);
        Children.Add(_indicator);
        Children.Add(_contentHost);
        Focusable = true;
        ClipToBounds = true;
    }

    /// <summary>Gets or sets a node, text, or object to display, or null for indicator-only content.</summary>
    /// <remarks>
    /// Other objects are displayed using ToString. Moving the displayed node elsewhere clears this property.
    /// Changing content during its conversion, tree changes, or notifications is rejected; same-reference confirmation is allowed.
    /// </remarks>
    public object? Content
    {
        get => _content;
        set
        {
            VerifyMutationAccess();
            if (ContentProperty.ValuesEqual(_content, value))
                return;
            if (_changingContent)
                throw new InvalidOperationException("Radio button content cannot change during its update or notifications.");
            _changingContent = true;
            try
            {
                if (value is Node node)
                    ValidateContentAssignment(node);
                SetField(ContentProperty, ref _content, value);
            }
            finally
            {
                _changingContent = false;
            }
        }
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

    /// <summary>Gets or sets the spacing between the indicator and non-null content in logical pixels.</summary>
    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureContent(Size availableSize)
    {
        var gap = _contentNode is null ? 0 : GetLayoutSpacing();
        _indicator.Measure(availableSize);
        var width = double.IsPositiveInfinity(availableSize.Width)
            ? double.PositiveInfinity
            : Math.Max(0, availableSize.Width - _indicator.DesiredSize.Width - gap);
        _contentHost.Measure(new Size(width, availableSize.Height));
        return new Size(_indicator.DesiredSize.Width + gap + _contentHost.DesiredSize.Width,
            Math.Max(_indicator.DesiredSize.Height, _contentHost.DesiredSize.Height));
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
        var gap = _contentNode is null ? 0 : GetLayoutSpacing();
        var offset = Math.Min(contentBounds.Width, indicatorWidth + gap);
        _contentHost.Arrange(new Rect(contentBounds.X + offset, contentBounds.Y,
            Math.Max(0, contentBounds.Width - offset), contentBounds.Height));
    }

    /// <inheritdoc />
    protected override bool IsPressedPseudoClassActive => IsPressed || _spaceKeyDown;

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerButtonEventArgs eventArgs)
    {
        base.OnPointerPressed(eventArgs);
        if (!eventArgs.Handled && eventArgs.Button == MouseButton.Left && HasInputQualification())
            eventArgs.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerButtonEventArgs eventArgs)
    {
        base.OnPointerReleased(eventArgs);
        if (!eventArgs.Handled && eventArgs.Button == MouseButton.Left && HasInputQualification())
            eventArgs.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerClicked(PointerButtonEventArgs eventArgs)
    {
        base.OnPointerClicked(eventArgs);
        if (eventArgs.Handled || eventArgs.Button != MouseButton.Left || !HasInputQualification())
            return;
        eventArgs.Handled = true;
        SelectFromInput();
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs eventArgs)
    {
        var focusVersion = _focusVersion;
        var eligible = IsFocused && Focusable && HasInputQualification();
        var activateEnter = eligible && !eventArgs.IsRepeat && !_enterKeyDown;
        if (eligible && !eventArgs.IsRepeat)
        {
            if (eventArgs.Key == Key.Enter)
                _enterKeyDown = true;
            else if (eventArgs.Key == Key.Space)
                SetSpaceKeyDown(true);
        }

        base.OnKeyDown(eventArgs);
        if (eventArgs.Handled || focusVersion != _focusVersion || !IsFocused || !Focusable || !HasInputQualification())
        {
            if (eventArgs.Key == Key.Space)
                SetSpaceKeyDown(false);
            return;
        }

        switch (eventArgs.Key)
        {
            case Key.Enter:
                eventArgs.Handled = true;
                if (activateEnter)
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
        var focusVersion = _focusVersion;
        var activateSpace = _spaceKeyDown;
        if (eventArgs.Key == Key.Enter)
            _enterKeyDown = false;
        else if (eventArgs.Key == Key.Space)
            SetSpaceKeyDown(false);
        base.OnKeyUp(eventArgs);
        if (eventArgs.Handled || focusVersion != _focusVersion || !IsFocused || !Focusable || !HasInputQualification())
            return;
        if (eventArgs.Key is Key.Enter or Key.Space)
        {
            eventArgs.Handled = true;
            if (eventArgs.Key == Key.Space && activateSpace)
                SelectFromInput();
        }
    }

    /// <inheritdoc />
    protected override void OnLostFocus(FocusChangedEventArgs eventArgs)
    {
        _focusVersion++;
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

    private void SynchronizeContent(object? content)
    {
        _contentNode = null;
        _contentNode = CreateContentNode(content);
        _contentHost.SetContent(_contentNode);
        InvalidateMeasure();
    }

    private void NotifyContentDetached(Node node)
    {
        if (!ReferenceEquals(node, _contentNode))
            return;
        var wasChangingContent = _changingContent;
        _changingContent = true;
        try
        {
            SetField(ContentProperty, ref _content, null);
        }
        finally
        {
            _changingContent = wasChangingContent;
        }
    }

    private void ValidateContentAssignment(Node value)
    {
        value.Screen?.VerifyTreeMutationAccess();
        for (Node? ancestor = this; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ReferenceEquals(ancestor, value))
                throw new InvalidOperationException("Radio button content cannot create a parent cycle.");
        }
        for (var part = value; part is not null; part = part.Parent)
        {
            if (part is RadioButtonIndicator || value is RadioButtonContentHost)
                throw new InvalidOperationException("An internal radio button part cannot be used as content.");
        }
        if (value.Parent is null && value.Screen is not null)
            throw new InvalidOperationException("A UI screen root must be cleared before it can be radio button content.");
    }

    private static Node? CreateContentNode(object? content)
    {
        if (content is null)
            return null;
        if (content is Node node)
            return node;
        var text = new Text
        {
            Content = content as string ?? content.ToString() ?? string.Empty,
            Wrapping = TextWrapping.NoWrap,
            IsHitTestVisible = false
        };
        text.Classes.Add("radio-button-text");
        return text;
    }

    private double GetLayoutSpacing()
    {
        var screen = Screen;
        return screen?.UseLayoutRounding ?? true
            ? LayoutHelper.RoundLayoutValue(Spacing, screen?.Scale ?? 1)
            : Spacing;
    }

    /// <summary>Determines whether this button can receive user input on its current screen.</summary>
    private protected bool HasInputQualification()
    {
        var screen = Screen;
        if (screen is null || !screen.IsOpen())
            return false;
        for (Node? node = this; node is not null; node = node.Parent)
        {
            if (!node.IsEnabled || node.Visibility != Visibility.Visible)
                return false;
            if (ReferenceEquals(node, screen.Root))
                return true;
        }
        return false;
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
            onChanged: static (button, oldGroup, newGroup) => RadioGroup<T>.SynchronizeMembership(button, oldGroup, newGroup));

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
        var screen = Screen;
        var members = group.GetMembersSnapshot();
        var current = Array.IndexOf(members, this);
        for (var offset = 1; offset < members.Length; offset++)
        {
            var index = (current + direction * offset + members.Length) % members.Length;
            var candidate = members[index];
            if (!ReferenceEquals(candidate.Group, group) || candidate.Value is null ||
                !ReferenceEquals(candidate.Screen, screen) || !candidate.Focus())
                continue;
            if (ReferenceEquals(_group, group) && ReferenceEquals(candidate.Group, group) &&
                ReferenceEquals(candidate.Screen, screen) && candidate.IsFocused && candidate.Focusable &&
                candidate.Value is not null && candidate.HasInputQualification())
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

internal sealed class RadioButtonContentHost : Control
{
    internal RadioButtonContentHost(Action<Node> notifyDetached)
    {
        Classes.Add("radio-button-content");
        Children.Changed += (_, change) =>
        {
            foreach (var child in change.OldItems)
            {
                if (!ReferenceEquals(child.Parent, this))
                    notifyDetached(child);
            }
        };
    }

    internal void SetContent(Node? content)
    {
        if (Children.Count > 0 && !ReferenceEquals(Children[0], content))
            Children.Remove(Children[0]);
        if (content is not null && !ReferenceEquals(content.Parent, this))
            Children.Add(content);
    }

    protected override Size MeasureContent(Size availableSize)
    {
        if (Children.Count == 0)
            return Size.Zero;
        var content = Children[0];
        content.Measure(availableSize);
        return content.DesiredSize;
    }

    protected override void ArrangeContent(Rect contentBounds)
    {
        if (Children.Count > 0)
            Children[0].Arrange(contentBounds);
    }
}

using System.Globalization;
using System.Numerics;
using PanguEngine.ComponentModel;
using PanguEngine.Input;
using PanguEngine.UI.Input;
using PanguEngine.UI.Styling;

namespace PanguEngine.UI.Controls;

/// <summary>Provides shared formatting and read-only settings for numeric controls.</summary>
public abstract class NumericUpDown : Control
{
    /// <summary>Identifies the <see cref="FormatString"/> property.</summary>
    public static readonly Property<string> FormatStringProperty =
        Property.Register<NumericUpDown, string>(nameof(FormatString), string.Empty,
            onChanged: static (node, _, _) => node.OnFormattingChanged());

    /// <summary>Identifies the <see cref="FormatProvider"/> property.</summary>
    public static readonly Property<IFormatProvider> FormatProviderProperty =
        Property.Register<NumericUpDown, IFormatProvider>(nameof(FormatProvider), CultureInfo.InvariantCulture,
            onChanged: static (node, _, _) => node.OnFormattingChanged());

    /// <summary>Identifies the <see cref="IsReadOnly"/> property.</summary>
    public static readonly Property<bool> IsReadOnlyProperty =
        Property.Register<NumericUpDown, bool>(nameof(IsReadOnly),
            onChanged: static (node, _, _) => node.OnReadOnlyChanged());

    static NumericUpDown() => CssRegistry.RegisterElement<NumericUpDown>("NumericUpDown");

    /// <summary>Initializes shared numeric control settings.</summary>
    protected NumericUpDown()
    {
    }

    /// <summary>Gets or sets the numeric display format. An empty string selects the type's default format.</summary>
    public string FormatString
    {
        get => GetValue(FormatStringProperty);
        set => SetValue(FormatStringProperty, value);
    }

    /// <summary>Gets or sets the provider for numeric parsing and formatting.</summary>
    /// <remarks>
    /// Typed numeric controls initialize a local value from the creation culture.
    /// Clearing it restores the committed style or invariant culture default.
    /// </remarks>
    public IFormatProvider FormatProvider
    {
        get => GetValue(FormatProviderProperty);
        set => SetValue(FormatProviderProperty, value);
    }

    /// <summary>Gets or sets whether users may change the value. Selection and copying remain available.</summary>
    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>Responds to changes in numeric formatting settings.</summary>
    protected virtual void OnFormattingChanged()
    {
    }

    /// <summary>Responds to changes in the read-only setting.</summary>
    protected virtual void OnReadOnlyChanged()
    {
    }
}

/// <summary>Edits a typed number within a finite range and supports incremental changes.</summary>
/// <typeparam name="T">A numeric type that can represent the default values zero, one, and one hundred.</typeparam>
public sealed class NumericUpDown<T> : NumericUpDown where T : INumber<T>
{
    private static readonly T DefaultMaximum = T.CreateChecked(100);

    /// <summary>Identifies the <see cref="Value"/> property.</summary>
    public static readonly DirectProperty<NumericUpDown<T>, T> ValueProperty =
        Property.RegisterDirect<NumericUpDown<T>, T>(nameof(Value), static node => node.Value,
            static (node, value) => node.Value = value, unsetValue: T.Zero,
            onChanged: static (node, _, _) => node.OnValueChanged());

    /// <summary>Identifies the <see cref="Minimum"/> property.</summary>
    public static readonly DirectProperty<NumericUpDown<T>, T> MinimumProperty =
        Property.RegisterDirect<NumericUpDown<T>, T>(nameof(Minimum), static node => node.Minimum,
            static (node, value) => node.Minimum = value, unsetValue: T.Zero,
            onChanged: static (node, _, _) => node.OnRangeChanged(true));

    /// <summary>Identifies the <see cref="Maximum"/> property.</summary>
    public static readonly DirectProperty<NumericUpDown<T>, T> MaximumProperty =
        Property.RegisterDirect<NumericUpDown<T>, T>(nameof(Maximum), static node => node.Maximum,
            static (node, value) => node.Maximum = value, unsetValue: DefaultMaximum,
            onChanged: static (node, _, _) => node.OnRangeChanged(false));

    /// <summary>Identifies the <see cref="Increment"/> property.</summary>
    public static readonly Property<T> IncrementProperty = Property.Register<NumericUpDown<T>, T>(
        nameof(Increment), T.One,
        onChanged: static (node, _, _) =>
        {
            node.CancelRepeat();
            node._wheelRemainder = 0;
            node.UpdatePartStates();
        },
        validate: static value => T.IsFinite(value) && value >= T.Zero,
        validationMessage: "Increment must be finite and non-negative.");

    private readonly TextBox _editor = new();
    private readonly NumericUpDownPart _increase = new(true);
    private readonly NumericUpDownPart _decrease = new(false);
    private T _minimum = T.Zero;
    private T _maximum = DefaultMaximum;
    private T _value = T.Zero;
    private bool _editing;
    private bool _writingDisplay;
    private bool _userModified;
    private readonly Ticker _repeatTicker;
    private NumericUpDownPart? _pressedPart;
    private bool _hasRepeatBaseline;
    private TimeSpan _lastRepeatTime;
    private TimeSpan _repeatAccumulated;
    private TimeSpan _nextRepeatDelay = TimeSpan.FromMilliseconds(400);
    private double _wheelRemainder;

    /// <summary>Initializes an editable numeric control with the default range zero to one hundred.</summary>
    public NumericUpDown()
    {
        Focusable = true;
        _repeatTicker = CreateTicker(OnRepeatTick);
        _editor.Classes.Add("numeric-up-down-editor");
        Children.Add(_editor);
        Children.Add(_increase);
        Children.Add(_decrease);
        _editor.PropertyChanged += OnEditorPropertyChanged;
        _editor.GotFocus += OnEditorGotFocus;
        _editor.LostFocus += OnEditorLostFocus;
        _increase.PropertyChanged += OnPartPropertyChanged;
        _decrease.PropertyChanged += OnPartPropertyChanged;
        FormatProvider = CultureInfo.CurrentCulture;
        ShowValue();
        UpdatePartStates();
        Subscribe(IsPressedProperty, (_, _) =>
        {
            if (!IsPressed)
                CancelRepeat();
        });
        Subscribe(IsEnabledProperty, (_, _) =>
        {
            if (!IsEnabled)
            {
                CancelRepeat();
                _wheelRemainder = 0;
            }
        });
        Subscribe(VisibilityProperty, (_, _) =>
        {
            if (Visibility != Visibility.Visible)
            {
                CancelRepeat();
                _wheelRemainder = 0;
            }
        });
        Subscribe(SceneProperty, (_, _) =>
        {
            CancelRepeat();
            _wheelRemainder = 0;
        });
    }

    /// <summary>Gets or sets the finite value, clamped to the current range.</summary>
    /// <exception cref="ArgumentException">The supplied value is not finite.</exception>
    public T Value
    {
        get => _value;
        set
        {
            EnsureFinite(value, nameof(Value));
            SetField(ValueProperty, ref _value, Clamp(value));
        }
    }

    /// <summary>Gets or sets the finite lower boundary, raising the upper boundary when necessary.</summary>
    /// <exception cref="ArgumentException">The supplied value is not finite.</exception>
    public T Minimum
    {
        get => _minimum;
        set
        {
            EnsureFinite(value, nameof(Minimum));
            SetField(MinimumProperty, ref _minimum, value);
        }
    }

    /// <summary>Gets or sets the finite upper boundary, lowering the lower boundary when necessary.</summary>
    /// <exception cref="ArgumentException">The supplied value is not finite.</exception>
    public T Maximum
    {
        get => _maximum;
        set
        {
            EnsureFinite(value, nameof(Maximum));
            SetField(MaximumProperty, ref _maximum, value);
        }
    }

    /// <summary>Gets or sets the finite non-negative step size. Zero disables incremental input.</summary>
    /// <exception cref="ArgumentException">The supplied value is not finite or is negative.</exception>
    public T Increment
    {
        get => GetValue(IncrementProperty);
        set => SetValue(IncrementProperty, value);
    }

    /// <inheritdoc />
    protected internal override UIElement GetFocusTarget() => _editor;

    private void OnValueChanged()
    {
        if (!_editing)
            ShowValue();
        UpdatePartStates();
    }

    private void OnRangeChanged(bool minimumChanged)
    {
        var oldMinimum = _minimum;
        var oldMaximum = _maximum;
        var oldValue = _value;
        if (minimumChanged)
            _maximum = T.Max(_minimum, _maximum);
        else
            _minimum = T.Min(_minimum, _maximum);
        _value = Clamp(_value);
        var newMinimum = _minimum;
        var newMaximum = _maximum;
        if (!ValueProperty.ValuesEqual(oldValue, _value))
            RaisePropertyChanged(ValueProperty, oldValue, _value);
        if (!MinimumProperty.ValuesEqual(oldMinimum, newMinimum))
            RaisePropertyChanged(MinimumProperty, oldMinimum, newMinimum);
        if (!MaximumProperty.ValuesEqual(oldMaximum, newMaximum))
            RaisePropertyChanged(MaximumProperty, oldMaximum, newMaximum);
        UpdatePartStates();
    }

    private T Clamp(T value) => value < _minimum ? _minimum : value > _maximum ? _maximum : value;

    private static void EnsureFinite(T value, string propertyName)
    {
        if (!T.IsFinite(value))
            throw new ArgumentException("The value must be finite.", propertyName);
    }

    /// <inheritdoc />
    protected override void OnFormattingChanged()
    {
        if (!_editing)
            ShowValue();
        UpdatePartStates();
    }

    /// <inheritdoc />
    protected override void OnReadOnlyChanged()
    {
        CancelRepeat();
        _wheelRemainder = 0;
        _editor.IsReadOnly = IsReadOnly;
        SetPseudoClass(PseudoClass.ReadOnly, IsReadOnly);
        _editing = false;
        ShowValue();
        _editing = _editor.IsFocused && !IsReadOnly;
        UpdatePartStates();
    }

    private void UpdatePartStates()
    {
        var candidate = GetConfirmationCandidate();
        var enabled = !IsReadOnly && Increment > T.Zero;
        _increase.IsEnabled = enabled && candidate < _maximum;
        _decrease.IsEnabled = enabled && candidate > _minimum;
    }

    private void OnEditorPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (!ReferenceEquals(eventArgs.Property, TextBox.TextProperty) || _writingDisplay || !_editing)
            return;

        _userModified = true;
        if (T.TryParse(_editor.Text, FormatProvider, out var parsed) &&
            T.IsFinite(parsed) && parsed >= _minimum && parsed <= _maximum)
        {
            Value = parsed;
        }

        UpdatePartStates();
    }

    private void OnEditorGotFocus(object? sender, FocusChangedEventArgs eventArgs)
    {
        _editing = !IsReadOnly;
    }

    private void OnEditorLostFocus(object? sender, FocusChangedEventArgs eventArgs)
    {
        if (ReferenceEquals(_editor.Scene?.FocusedNode, _editor))
            return;

        CancelRepeat();
        _wheelRemainder = 0;
        if (_editing)
            ConfirmText();
        _editing = false;
    }

    private T GetConfirmationCandidate() =>
        _userModified && T.TryParse(_editor.Text, FormatProvider, out var parsed) && T.IsFinite(parsed)
            ? Clamp(parsed)
            : _value;

    private void ConfirmText()
    {
        Value = GetConfirmationCandidate();
        ShowValue();
        UpdatePartStates();
    }

    private void ShowValue()
    {
        var text = _value.ToString(FormatString, FormatProvider);
        _writingDisplay = true;
        try
        {
            _editor.Text = text;
            _userModified = false;
        }
        finally
        {
            _writingDisplay = false;
        }
    }

    private bool CanStep => !IsReadOnly && Increment > T.Zero && IsEnabled;

    private bool Step(bool increase)
    {
        ConfirmText();
        if (!CanStep)
            return false;
        var previous = _value;
        T result;
        try
        {
            result = increase ? checked(previous + Increment) : checked(previous - Increment);
        }
        catch (OverflowException)
        {
            result = increase ? _maximum : _minimum;
        }

        if (!T.IsFinite(result))
        {
            if (increase && T.IsPositiveInfinity(result))
                result = _maximum;
            else if (!increase && T.IsNegativeInfinity(result))
                result = _minimum;
            else
                throw new InvalidOperationException("The numeric step produced an invalid non-finite result.");
        }

        Value = result;
        ShowValue();
        UpdatePartStates();
        return _value != previous;
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs eventArgs)
    {
        base.OnKeyDown(eventArgs);
        if (eventArgs.Handled || !_editor.IsFocused || IsReadOnly)
            return;

        switch (eventArgs.Key)
        {
            case Key.Up when CanStep && eventArgs.Modifiers == KeyModifiers.None:
                Step(true);
                eventArgs.Handled = true;
                break;
            case Key.Down when CanStep && eventArgs.Modifiers == KeyModifiers.None:
                Step(false);
                eventArgs.Handled = true;
                break;
            case Key.Enter:
                ConfirmText();
                eventArgs.Handled = true;
                break;
            case Key.Escape:
                ShowValue();
                UpdatePartStates();
                eventArgs.Handled = true;
                break;
        }
    }

    /// <inheritdoc />
    protected override void OnPointerWheel(PointerWheelEventArgs eventArgs)
    {
        base.OnPointerWheel(eventArgs);
        if (eventArgs.Handled || !_editor.IsFocused || !CanStep || eventArgs.DeltaY == 0)
            return;

        eventArgs.Handled = true;
        _wheelRemainder += eventArgs.DeltaY;
        while (_wheelRemainder >= 1 || _wheelRemainder <= -1)
        {
            var increase = _wheelRemainder > 0;
            _wheelRemainder += increase ? -1 : 1;
            if (!CanStep || !_editor.IsFocused || !Step(increase))
            {
                _wheelRemainder = 0;
                break;
            }
        }
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerButtonEventArgs eventArgs)
    {
        base.OnPointerPressed(eventArgs);
        if (eventArgs.Handled || eventArgs.Button != MouseButton.Left || !CanStep ||
            eventArgs.Source is not NumericUpDownPart part ||
            (!ReferenceEquals(part, _increase) && !ReferenceEquals(part, _decrease)))
            return;

        eventArgs.Handled = true;
        CancelRepeat();
        if (!Step(part.IsIncrease) || !CanStep || !part.IsEnabled || !part.IsPressed)
            return;

        _pressedPart = part;
        ResetRepeatDelay();
        _repeatTicker.Start();
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs eventArgs)
    {
        base.OnPointerMoved(eventArgs);
        if (_pressedPart is null)
            return;
        eventArgs.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerButtonEventArgs eventArgs)
    {
        base.OnPointerReleased(eventArgs);
        if (eventArgs.Button != MouseButton.Left)
            return;
        CancelRepeat();
        if (ReferenceEquals(eventArgs.Source, _increase) || ReferenceEquals(eventArgs.Source, _decrease))
            eventArgs.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerCanceled(PointerButtonEventArgs eventArgs)
    {
        base.OnPointerCanceled(eventArgs);
        if (eventArgs.Button == MouseButton.Left)
            CancelRepeat();
    }

    private void OnPartPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (_pressedPart is not { } part || !ReferenceEquals(sender, part))
            return;
        if (ReferenceEquals(eventArgs.Property, IsPressedProperty) && !part.IsPressed ||
            ReferenceEquals(eventArgs.Property, IsEnabledProperty) && !part.IsEnabled)
            CancelRepeat();
        else if (ReferenceEquals(eventArgs.Property, IsHoveredProperty))
            ResetRepeatDelay();
    }

    private void OnRepeatTick(TimeSpan frameTime)
    {
        if (_pressedPart is not { } part)
            return;
        if (!CanStep || !part.IsPressed || !part.IsEnabled || Scene is null)
        {
            CancelRepeat();
            return;
        }

        if (!part.IsHovered)
            return;
        if (!_hasRepeatBaseline)
        {
            _hasRepeatBaseline = true;
            _lastRepeatTime = frameTime;
            return;
        }

        var delta = frameTime - _lastRepeatTime;
        _lastRepeatTime = frameTime;
        if (delta <= TimeSpan.Zero)
            return;
        _repeatAccumulated += delta;
        if (_repeatAccumulated < _nextRepeatDelay)
            return;

        _repeatAccumulated = TimeSpan.Zero;
        _nextRepeatDelay = TimeSpan.FromMilliseconds(50);
        if (!Step(part.IsIncrease))
            CancelRepeat();
    }

    private void ResetRepeatDelay()
    {
        _hasRepeatBaseline = false;
        _repeatAccumulated = TimeSpan.Zero;
        _nextRepeatDelay = TimeSpan.FromMilliseconds(400);
    }

    private void CancelRepeat()
    {
        _pressedPart = null;
        ResetRepeatDelay();
        _repeatTicker.Stop();
    }

    /// <inheritdoc />
    protected override Size MeasureContent(Size availableSize)
    {
        var buttonSpace = new Size(availableSize.Width, availableSize.Height / 2);
        _increase.Measure(buttonSpace);
        _decrease.Measure(buttonSpace);
        var columnWidth = Math.Max(_increase.DesiredSize.Width, _decrease.DesiredSize.Width);
        _editor.Measure(new Size(Math.Max(0, availableSize.Width - columnWidth), availableSize.Height));
        return new Size(_editor.DesiredSize.Width + columnWidth,
            Math.Max(_editor.DesiredSize.Height, _increase.DesiredSize.Height + _decrease.DesiredSize.Height));
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Rect contentBounds)
    {
        var columnWidth = Math.Min(contentBounds.Width,
            Math.Max(_increase.DesiredSize.Width, _decrease.DesiredSize.Width));
        var editorWidth = Math.Max(0, contentBounds.Width - columnWidth);
        var halfHeight = contentBounds.Height / 2;
        _editor.Arrange(new Rect(contentBounds.X, contentBounds.Y, editorWidth, contentBounds.Height));
        _increase.Arrange(new Rect(contentBounds.X + editorWidth, contentBounds.Y, columnWidth, halfHeight));
        _decrease.Arrange(new Rect(contentBounds.X + editorWidth, contentBounds.Y + halfHeight,
            columnWidth, contentBounds.Height - halfHeight));
    }

    /// <summary>Provides a pointer target for increasing or decreasing a numeric value.</summary>
    private sealed class NumericUpDownPart : Control
    {
        private readonly Path _arrow = new() { IsHitTestVisible = false };

        static NumericUpDownPart() => CssRegistry.RegisterElement<NumericUpDownPart>("NumericUpDownPart");

        /// <summary>Initializes a numeric step target.</summary>
        /// <param name="isIncrease">Whether this target increases the value.</param>
        internal NumericUpDownPart(bool isIncrease)
        {
            IsIncrease = isIncrease;
            Classes.Add(isIncrease ? "numeric-up-down-increase" : "numeric-up-down-decrease");
            _arrow.Classes.Add("numeric-up-down-arrow");
            Children.Add(_arrow);
        }

        /// <summary>Gets whether this target increases the value.</summary>
        internal bool IsIncrease { get; }

        /// <inheritdoc />
        protected override Size MeasureContent(Size availableSize)
        {
            _arrow.Measure(availableSize);
            return _arrow.DesiredSize;
        }

        /// <inheritdoc />
        protected override void ArrangeContent(Rect contentBounds)
        {
            var width = Math.Min(contentBounds.Width, _arrow.DesiredSize.Width);
            var height = Math.Min(contentBounds.Height, _arrow.DesiredSize.Height);
            _arrow.Arrange(new Rect(contentBounds.X + (contentBounds.Width - width) / 2,
                contentBounds.Y + (contentBounds.Height - height) / 2, width, height));
        }
    }
}
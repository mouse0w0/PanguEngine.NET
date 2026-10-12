using System.Numerics;
using PanguEngine.Collections;
using PanguEngine.ComponentModel;

namespace PanguEngine.Client.UI.Controls;

/// <summary>Defines a continuous or stepped numeric range for a slider.</summary>
/// <typeparam name="T">The numeric value type.</typeparam>
public sealed class NumericSliderValueModel<T> : SliderValueModel<T> where T : INumber<T>
{
    /// <summary>Identifies the minimum value.</summary>
    public static readonly Property<T> MinimumProperty = Property.Register<NumericSliderValueModel<T>, T>(
        nameof(Minimum), T.Zero, onChanged: static (model, _, _) => model.NotifyValueDomainChanged(),
        validate: T.IsFinite, validationMessage: "Minimum must be finite.");

    /// <summary>Identifies the maximum value.</summary>
    public static readonly Property<T> MaximumProperty = Property.Register<NumericSliderValueModel<T>, T>(
        nameof(Maximum), T.CreateChecked(100), onChanged: static (model, _, _) => model.NotifyValueDomainChanged(),
        validate: T.IsFinite, validationMessage: "Maximum must be finite.");

    /// <summary>Identifies the user adjustment step.</summary>
    public static readonly Property<T> StepProperty = RegisterNonNegative(nameof(Step), T.Zero, true);

    /// <summary>Identifies the continuous keyboard adjustment amount.</summary>
    public static readonly Property<T> SmallChangeProperty = RegisterNonNegative(nameof(SmallChange), T.One, false);

    /// <summary>Identifies the page adjustment amount.</summary>
    public static readonly Property<T> LargeChangeProperty = RegisterNonNegative(nameof(LargeChange), T.CreateChecked(10), false);

    /// <summary>Identifies the automatically generated tick interval.</summary>
    public static readonly Property<T> TickFrequencyProperty = RegisterNonNegative(nameof(TickFrequency), T.Zero, false);

    /// <summary>Identifies the programmatic assignment policy.</summary>
    public static readonly Property<SliderValueAssignmentPolicy> AssignmentPolicyProperty =
        Property.Register<NumericSliderValueModel<T>, SliderValueAssignmentPolicy>(nameof(AssignmentPolicy),
            onChanged: static (model, _, _) => model.NotifyDisplayChanged());

    /// <summary>Identifies the explicit display tick collection.</summary>
    public static readonly DirectProperty<NumericSliderValueModel<T>, IObservableList<T>> TickValuesProperty =
        Property.RegisterDirect<NumericSliderValueModel<T>, IObservableList<T>>(nameof(TickValues),
            static model => model.TickValues, static (model, value) => model.TickValues = value,
            onChanged: static (model, _, value) =>
            {
                model._tickSubscription.Detach();
                model._tickSubscription = model.SubscribeTicks(value);
                model.NotifyDisplayChanged();
            },
            comparer: ReferenceEqualityComparer.Instance);

    // ReSharper disable once StaticMemberInGenericType
    private static readonly bool Integral = T.IsInteger(T.CreateSaturating(.5));
    private IObservableList<T> _tickValues = new ObservableList<T>();
    private SliderListSubscription<NumericSliderValueModel<T>, T> _tickSubscription;

    /// <summary>Creates a numeric range from zero to one hundred.</summary>
    public NumericSliderValueModel() => _tickSubscription = SubscribeTicks(_tickValues);

    /// <summary>Gets or sets the range minimum.</summary>
    public T Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

    /// <summary>Gets or sets the range maximum.</summary>
    public T Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    /// <summary>Gets or sets the user adjustment step, or zero for continuous adjustment.</summary>
    public T Step { get => GetValue(StepProperty); set => SetValue(StepProperty, value); }

    /// <summary>Gets or sets the continuous keyboard adjustment amount.</summary>
    public T SmallChange { get => GetValue(SmallChangeProperty); set => SetValue(SmallChangeProperty, value); }

    /// <summary>Gets or sets the page adjustment amount.</summary>
    public T LargeChange { get => GetValue(LargeChangeProperty); set => SetValue(LargeChangeProperty, value); }

    /// <summary>Gets or sets the display tick interval, or zero to display only endpoints.</summary>
    public T TickFrequency { get => GetValue(TickFrequencyProperty); set => SetValue(TickFrequencyProperty, value); }

    /// <summary>Gets or sets the policy applied to future programmatic assignments.</summary>
    public SliderValueAssignmentPolicy AssignmentPolicy { get => GetValue(AssignmentPolicyProperty); set => SetValue(AssignmentPolicyProperty, value); }

    /// <summary>Gets or sets explicit display tick values, which override the interval when nonempty.</summary>
    public IObservableList<T> TickValues
    {
        get => _tickValues;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            SetField(TickValuesProperty, ref _tickValues, value);
        }
    }

    private T EffectiveMaximum => T.Max(Minimum, Maximum);
    private SliderNumber Start => SliderNumber.From(Minimum);
    private SliderNumber End => SliderNumber.From(EffectiveMaximum);

    /// <inheritdoc />
    public override SliderValueState<T> CreateInitialState() => new(Minimum);

    /// <inheritdoc />
    public override SliderValueState<T> CreateReplacementState(SliderValueState<T> previous)
    {
        var resolved = base.CreateReplacementState(previous);
        return previous.HasValue && resolved.Value != previous.Value ? CreateInitialState() : resolved;
    }

    /// <inheritdoc />
    public override SliderValueState<T> ResolveValue(T value)
    {
        if (!T.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value), "The value must be finite.");
        var clamped = T.Clamp(value, Minimum, EffectiveMaximum);
        if (AssignmentPolicy == SliderValueAssignmentPolicy.Clamp)
            return new SliderValueState<T>(clamped);
        var snapped = Snap(SliderNumber.From(clamped));
        if (AssignmentPolicy == SliderValueAssignmentPolicy.Reject && (value != clamped || value != snapped))
            throw new ArgumentException("The value is outside the range or between legal steps.", nameof(value));
        return new SliderValueState<T>(snapped);
    }

    /// <inheritdoc />
    public override SliderValueState<T> ResolvePosition(double position)
    {
        if (position <= 0)
            return new SliderValueState<T>(Minimum);
        if (position >= 1)
            return new SliderValueState<T>(EffectiveMaximum);
        return new SliderValueState<T>(Snap(Start + (End - Start) * SliderNumber.From(position)));
    }

    /// <inheritdoc />
    public override SliderValueState<T> Move(SliderValueState<T> state, int direction, bool page)
    {
        if (direction == 0)
            return state;
        var current = SliderNumber.From(state.Value);
        if (page)
        {
            var amount = SliderNumber.From(T.Max(LargeChange, Step));
            var value = Snap(current + amount * new SliderNumber(direction > 0 ? 1 : -1));
            if (Step > T.Zero && value == state.Value)
                value = NextSteppedValue(state.Value, direction);
            return new SliderValueState<T>(value);
        }
        if (Step == T.Zero)
        {
            var amount = SliderNumber.From(SmallChange);
            return new SliderValueState<T>(ToClampedValue(current + amount * new SliderNumber(direction > 0 ? 1 : -1)));
        }
        return new SliderValueState<T>(NextSteppedValue(state.Value, direction));
    }

    /// <inheritdoc />
    public override SliderValueState<T> GetBoundary(bool last) => new(last ? EffectiveMaximum : Minimum);

    /// <inheritdoc />
    public override double GetPosition(SliderValueState<T> state) => Minimum == EffectiveMaximum
        ? .5
        : ((SliderNumber.From(state.Value) - Start) / (End - Start)).ToDouble();

    /// <inheritdoc />
    public override SliderValueState<T> RefreshState(SliderValueState<T> state, SliderValueModelChange<T> change)
    {
        if (!state.HasValue)
            return CreateInitialState();
        if (!change.AffectsValues)
            return state;
        return new SliderValueState<T>(AssignmentPolicy == SliderValueAssignmentPolicy.Clamp
            ? T.Clamp(state.Value, Minimum, EffectiveMaximum)
            : Snap(SliderNumber.From(state.Value)));
    }

    /// <inheritdoc />
    public override IReadOnlyList<SliderTick<T>> GetTicks(int maximumCount)
    {
        var budget = Math.Clamp(maximumCount, 2, 2048);
        if (Minimum == EffectiveMaximum)
            return [new SliderTick<T>(Minimum, .5)];
        var values = new List<T>();
        if (_tickValues.Count > 0)
        {
            foreach (var value in _tickValues)
                if (T.IsFinite(value) && value >= Minimum && value <= EffectiveMaximum)
                    values.Add(value);
            values.Sort();
        }
        else
        {
            values.Add(Minimum);
            if (TickFrequency > T.Zero)
            {
                var frequency = SliderNumber.From(TickFrequency);
                var count = ((End - Start) / frequency).Floor();
                var samples = count < budget ? (int)count : budget - 1;
                for (var sample = 1; sample <= samples; sample++)
                {
                    var index = count < budget ? new BigInteger(sample) : count * sample / samples;
                    values.Add(ToClampedValue(Start + new SliderNumber(index) * frequency));
                }
            }
            values.Add(EffectiveMaximum);
        }
        var ticks = new List<SliderTick<T>>();
        var occupied = new HashSet<int>();
        foreach (var value in values)
        {
            var position = GetPosition(new SliderValueState<T>(value));
            var pixel = (int)Math.Round(position * (budget - 1));
            if (occupied.Add(pixel))
                ticks.Add(new SliderTick<T>(value, position));
            else if (value == EffectiveMaximum && ticks.Count > 0)
                ticks[^1] = new SliderTick<T>(value, position);
        }
        return ticks;
    }

    /// <inheritdoc />
    public override IReadOnlyList<SliderTick<T>> GetTicks(int maximumCount, SliderValueState<T> state)
    {
        var ticks = GetTicks(maximumCount);
        if (!state.HasValue)
            return ticks;
        var value = state.Value;
        var displayed = _tickValues.Count > 0 ? _tickValues.Contains(value) : value == Minimum || value == EffectiveMaximum;
        if (!displayed && _tickValues.Count == 0 && TickFrequency > T.Zero)
        {
            var frequency = SliderNumber.From(TickFrequency);
            var index = ((SliderNumber.From(value) - Start) / frequency).Floor();
            displayed = ToClampedValue(Start + new SliderNumber(index) * frequency) == value ||
                ToClampedValue(Start + new SliderNumber(index + 1) * frequency) == value;
        }
        return displayed ? IncludeSelectedTick(ticks, new SliderTick<T>(value, GetPosition(state))) : ticks;
    }

    private T NextSteppedValue(T currentValue, int direction)
    {
        var step = SliderNumber.From(Step);
        var quotient = (SliderNumber.From(currentValue) - Start) / step;
        var index = direction > 0 ? quotient.Floor() + 1 : quotient.Ceiling() - 1;
        var candidate = ToClampedValue(Start + new SliderNumber(index) * step);
        if (direction > 0 ? candidate > currentValue : candidate < currentValue)
            return candidate;
        var low = direction > 0 ? index + 1 : BigInteger.Zero;
        var high = direction > 0 ? ((End - Start) / step).Floor() : index - 1;
        var result = direction > 0 ? EffectiveMaximum : Minimum;
        while (low <= high)
        {
            var middle = (low + high) / 2;
            candidate = ToClampedValue(Start + new SliderNumber(middle) * step);
            var progresses = direction > 0 ? candidate > currentValue : candidate < currentValue;
            if (progresses)
                result = candidate;
            if (direction > 0 ? progresses : !progresses)
                high = middle - 1;
            else
                low = middle + 1;
        }
        return result;
    }

    private T Snap(SliderNumber value)
    {
        value = Clamp(value);
        if (Step == T.Zero || Minimum == EffectiveMaximum)
            return ToClampedValue(value);
        var step = SliderNumber.From(Step);
        var index = ((value - Start) / step).Floor();
        var lower = Start + new SliderNumber(index) * step;
        var upper = Clamp(lower + step);
        return ToClampedValue(value - lower < upper - value ? lower : upper);
    }

    private SliderNumber Clamp(SliderNumber value) => value < Start ? Start : value > End ? End : value;

    private T ToClampedValue(SliderNumber value)
    {
        if (value <= Start)
            return Minimum;
        if (value >= End)
            return EffectiveMaximum;
        return T.Clamp(value.ToValue<T>(Integral), Minimum, EffectiveMaximum);
    }

    private static Property<T> RegisterNonNegative(string name, T defaultValue, bool affectsValues) =>
        Property.Register<NumericSliderValueModel<T>, T>(name, defaultValue,
            onChanged: (model, _, _) =>
            {
                if (affectsValues)
                    model.NotifyValueDomainChanged();
                else
                    model.NotifyDisplayChanged();
            },
            validate: static value => T.IsFinite(value) && value >= T.Zero,
            validationMessage: $"{name} must be finite and non-negative.");

    private SliderListSubscription<NumericSliderValueModel<T>, T> SubscribeTicks(IObservableList<T> values) =>
        new(this, values, static (model, _) =>
        {
            model.VerifyMutationAccess();
            model.NotifyDisplayChanged();
        });
}

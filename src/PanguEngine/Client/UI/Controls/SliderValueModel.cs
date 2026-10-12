using PanguEngine.Collections;
using PanguEngine.ComponentModel;

namespace PanguEngine.Client.UI.Controls;

/// <summary>Describes one slider's effective value and optional list position.</summary>
/// <typeparam name="T">The value type.</typeparam>
public readonly record struct SliderValueState<T>
{
    /// <summary>Creates a state containing a value.</summary>
    /// <param name="value">The effective value.</param>
    /// <param name="index">The list position, or -1 for a numeric value.</param>
    public SliderValueState(T value, int index = -1)
    {
        HasValue = true;
        Value = value;
        Index = index;
    }

    /// <summary>Gets whether this state contains a value.</summary>
    public bool HasValue { get; }

    /// <summary>Gets the effective value.</summary>
    /// <exception cref="InvalidOperationException">The state has no value.</exception>
    public T Value => HasValue ? field : throw new InvalidOperationException("The slider has no value.");

    /// <summary>Gets the list position when this state contains a list value.</summary>
    public int Index { get; }
}

/// <summary>Describes a tick supplied by a slider value model.</summary>
/// <typeparam name="T">The value type.</typeparam>
/// <param name="Value">The tick value.</param>
/// <param name="Position">The normalized track position.</param>
/// <param name="Index">The list position, or -1 for a numeric tick.</param>
public readonly record struct SliderTick<T>(T Value, double Position, int Index = -1);

/// <summary>Describes a value domain or display configuration change.</summary>
/// <typeparam name="T">The value type.</typeparam>
/// <param name="AffectsValues">Whether existing selections must be refreshed.</param>
/// <param name="ListChange">The candidate list change, when present.</param>
public readonly record struct SliderValueModelChange<T>(bool AffectsValues, ListChangedEventArgs<T>? ListChange = null);

/// <summary>Defines legal slider values, position conversions and adjustment rules.</summary>
/// <typeparam name="T">The value type.</typeparam>
public abstract class SliderValueModel<T> : ObservableObject
{
    internal SliderModelBinding<T> Connections { get; } = new();

    /// <summary>Creates the initial selection, which may be empty.</summary>
    public abstract SliderValueState<T> CreateInitialState();

    /// <summary>Creates a selection after replacing a model, preserving an accepted value when possible.</summary>
    /// <param name="previous">The previous selection.</param>
    public virtual SliderValueState<T> CreateReplacementState(SliderValueState<T> previous)
    {
        var initial = CreateInitialState();
        if (!previous.HasValue || !initial.HasValue)
            return initial;
        try
        {
            return ResolveValue(previous.Value);
        }
        catch (ArgumentException)
        {
            return initial;
        }
    }

    /// <summary>Resolves a programmatic value using the model's assignment rules.</summary>
    /// <param name="value">The requested value.</param>
    public abstract SliderValueState<T> ResolveValue(T value);

    /// <summary>Resolves a pointer position using the model's user adjustment rules.</summary>
    /// <param name="position">The normalized track position.</param>
    public abstract SliderValueState<T> ResolvePosition(double position);

    /// <summary>Moves a selection in the requested direction.</summary>
    /// <param name="state">The current selection.</param>
    /// <param name="direction">A negative value decreases and a positive value increases.</param>
    /// <param name="page">Whether to use the page adjustment amount.</param>
    public abstract SliderValueState<T> Move(SliderValueState<T> state, int direction, bool page);

    /// <summary>Gets the first or last legal selection.</summary>
    /// <param name="last">Whether to select the last value.</param>
    public abstract SliderValueState<T> GetBoundary(bool last);

    /// <summary>Gets a selection's normalized track position.</summary>
    /// <param name="state">The selection.</param>
    public abstract double GetPosition(SliderValueState<T> state);

    /// <summary>Gets display ticks within a visible tick budget.</summary>
    /// <param name="maximumCount">The preferred maximum number of visible ticks.</param>
    public abstract IReadOnlyList<SliderTick<T>> GetTicks(int maximumCount);

    /// <summary>Gets display ticks while retaining the selected display tick when available.</summary>
    /// <param name="maximumCount">The preferred maximum number of visible ticks.</param>
    /// <param name="state">The current selection.</param>
    public virtual IReadOnlyList<SliderTick<T>> GetTicks(int maximumCount, SliderValueState<T> state) => GetTicks(maximumCount);

    /// <summary>Includes a selected display tick in a bounded tick plan.</summary>
    /// <param name="ticks">The display tick plan.</param>
    /// <param name="selected">The selected display tick.</param>
    protected static IReadOnlyList<SliderTick<T>> IncludeSelectedTick(IReadOnlyList<SliderTick<T>> ticks, SliderTick<T> selected) =>
        ticks.Where(tick => !tick.Position.Equals(selected.Position)).Append(selected).OrderBy(tick => tick.Position).ToArray();

    /// <summary>Refreshes an existing selection after a model change.</summary>
    /// <param name="state">The previous selection.</param>
    /// <param name="change">The completed model change.</param>
    public abstract SliderValueState<T> RefreshState(SliderValueState<T> state, SliderValueModelChange<T> change);

    /// <summary>Updates connected selections after the value domain changes.</summary>
    /// <param name="listChange">The candidate list change, when present.</param>
    protected void NotifyValueDomainChanged(ListChangedEventArgs<T>? listChange = null) =>
        Connections.Update(new SliderValueModelChange<T>(true, listChange));

    /// <summary>Refreshes connected controls after the display configuration changes.</summary>
    protected void NotifyDisplayChanged() => Connections.Update(new SliderValueModelChange<T>(false));

    /// <inheritdoc />
    protected override void VerifyMutationAccess() => Connections.VerifyAccess();
}

internal sealed class SliderListSubscription<TTarget, TItem> where TTarget : class
{
    private readonly WeakReference<TTarget> _target;
    private readonly IReadOnlyObservableList<TItem> _source;
    private readonly Action<TTarget, ListChangedEventArgs<TItem>> _callback;
    private bool _isAttached = true;

    internal SliderListSubscription(TTarget target, IReadOnlyObservableList<TItem> source,
        Action<TTarget, ListChangedEventArgs<TItem>> callback)
    {
        _target = new WeakReference<TTarget>(target);
        _source = source;
        _callback = callback;
        source.Changed += OnChanged;
    }

    internal void Detach()
    {
        _isAttached = false;
        _source.Changed -= OnChanged;
    }

    private void OnChanged(object? sender, ListChangedEventArgs<TItem> change)
    {
        if (!_isAttached)
            return;
        if (_target.TryGetTarget(out var target))
            _callback(target, change);
        else
            Detach();
    }
}

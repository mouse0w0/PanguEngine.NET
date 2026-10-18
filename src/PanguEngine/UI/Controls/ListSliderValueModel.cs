using PanguEngine.Collections;
using PanguEngine.ComponentModel;

namespace PanguEngine.UI.Controls;

/// <summary>Defines equally spaced slider choices from a mutable observable list.</summary>
/// <typeparam name="T">The candidate value type.</typeparam>
public sealed class ListSliderValueModel<T> : SliderValueModel<T>
{
    /// <summary>Identifies the candidate collection.</summary>
    public static readonly DirectProperty<ListSliderValueModel<T>, IObservableList<T>> ItemsProperty =
        Property.RegisterDirect<ListSliderValueModel<T>, IObservableList<T>>(nameof(Items),
            static model => model.Items, static (model, value) => model.Items = value,
            onChanged: static (model, _, value) =>
            {
                model._subscription.Detach();
                model._subscription = model.SubscribeItems(value);
                model.NotifyValueDomainChanged();
            },
            comparer: ReferenceEqualityComparer.Instance);

    /// <summary>Identifies the number of choices moved by a page adjustment.</summary>
    public static readonly Property<int> PageStepProperty = Property.Register<ListSliderValueModel<T>, int>(
        nameof(PageStep), 3, onChanged: static (model, _, _) => model.NotifyDisplayChanged(),
        validate: static value => value >= 0, validationMessage: "PageStep must be non-negative.");

    private IObservableList<T> _items;
    private SliderListSubscription<ListSliderValueModel<T>, T> _subscription;

    /// <summary>Creates a model backed by the supplied candidate collection.</summary>
    /// <param name="items">The live candidate collection.</param>
    public ListSliderValueModel(IObservableList<T> items)
    {
        _items = items;
        _subscription = SubscribeItems(items);
    }

    /// <summary>Gets or sets the live candidate collection.</summary>
    public IObservableList<T> Items
    {
        get => _items;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            SetField(ItemsProperty, ref _items, value);
        }
    }

    /// <summary>Gets or sets the number of choices moved by a page adjustment.</summary>
    public int PageStep { get => GetValue(PageStepProperty); set => SetValue(PageStepProperty, value); }

    /// <inheritdoc />
    public override SliderValueState<T> CreateInitialState() => At(0);

    /// <inheritdoc />
    public override SliderValueState<T> ResolveValue(T value)
    {
        if (_items.Count == 0)
            throw new InvalidOperationException("The candidate list is empty.");
        var index = _items.IndexOf(value);
        if (index < 0)
            throw new ArgumentException("The value does not belong to the candidate list.", nameof(value));
        return At(index);
    }

    /// <inheritdoc />
    public override SliderValueState<T> ResolvePosition(double position) => _items.Count == 0
        ? default
        : At((int)Math.Floor(Math.Clamp(position, 0, 1) * (_items.Count - 1) + .5));

    /// <inheritdoc />
    public override SliderValueState<T> Move(SliderValueState<T> state, int direction, bool page) => _items.Count == 0
        ? default
        : At((int)Math.Clamp(state.Index + Math.Sign(direction) * (long)(page ? PageStep : 1), 0, _items.Count - 1));

    /// <inheritdoc />
    public override SliderValueState<T> GetBoundary(bool last) => At(last ? _items.Count - 1 : 0);

    /// <inheritdoc />
    public override double GetPosition(SliderValueState<T> state) => _items.Count <= 1 ? .5 : (double)state.Index / (_items.Count - 1);

    /// <inheritdoc />
    public override IReadOnlyList<SliderTick<T>> GetTicks(int maximumCount)
    {
        if (_items.Count == 0)
            return [];
        var budget = Math.Clamp(maximumCount, 2, 2048);
        var count = Math.Min(_items.Count, budget);
        var result = new List<SliderTick<T>>(count);
        for (var sample = 0; sample < count; sample++)
        {
            var index = count == 1 ? 0 : (int)((long)sample * (_items.Count - 1) / (count - 1));
            result.Add(new SliderTick<T>(_items[index], _items.Count == 1 ? .5 : (double)index / (_items.Count - 1), index));
        }
        return result;
    }

    /// <inheritdoc />
    public override IReadOnlyList<SliderTick<T>> GetTicks(int maximumCount, SliderValueState<T> state)
    {
        var ticks = GetTicks(maximumCount);
        return state.HasValue
            ? IncludeSelectedTick(ticks, new SliderTick<T>(state.Value, GetPosition(state), state.Index))
            : ticks;
    }

    /// <inheritdoc />
    public override SliderValueState<T> RefreshState(SliderValueState<T> state, SliderValueModelChange<T> change)
    {
        if (!change.AffectsValues)
            return state;
        if (_items.Count == 0)
            return default;
        if (!state.HasValue)
            return CreateInitialState();
        if (change.ListChange is not { } listChange)
        {
            var matched = _items.IndexOf(state.Value);
            return At(matched < 0 ? 0 : matched);
        }
        var index = state.Index;
        switch (listChange.Kind)
        {
            case ListChangeKind.Add:
                if (listChange.Index <= index)
                    index += listChange.NewItems.Count;
                break;
            case ListChangeKind.Remove:
                if (index >= listChange.Index + listChange.OldItems.Count)
                    index -= listChange.OldItems.Count;
                else if (index >= listChange.Index)
                    index = listChange.Index;
                break;
            case ListChangeKind.Replace:
                var oldCount = _items.Count - listChange.NewItems.Count + listChange.OldItems.Count;
                if (listChange.Index == 0 && listChange.OldItems.Count == oldCount)
                {
                    var matched = _items.IndexOf(state.Value);
                    if (matched >= 0)
                        index = matched;
                }
                else if (index >= listChange.Index + listChange.OldItems.Count)
                    index += listChange.NewItems.Count - listChange.OldItems.Count;
                else if (index >= listChange.Index)
                    index = listChange.Index + Math.Min(index - listChange.Index, Math.Max(0, listChange.NewItems.Count - 1));
                break;
            case ListChangeKind.Reorder:
                index = listChange.Permutation[index];
                break;
        }
        return At(Math.Min(index, _items.Count - 1));
    }

    private SliderValueState<T> At(int index) => _items.Count == 0 ? default : new SliderValueState<T>(_items[index], index);

    private SliderListSubscription<ListSliderValueModel<T>, T> SubscribeItems(IObservableList<T> items) =>
        new(this, items, static (model, change) =>
        {
            model.VerifyMutationAccess();
            model.NotifyValueDomainChanged(change);
        });
}

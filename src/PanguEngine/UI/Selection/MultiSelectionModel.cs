using PanguEngine.Collections;
using PanguEngine.ComponentModel;

namespace PanguEngine.UI.Selection;

/// <summary>Tracks selected occurrences in an observable source with configurable selection cardinality.</summary>
/// <typeparam name="T">The source item type.</typeparam>
/// <remarks>
/// Results are committed before notifications. Source collections must not be mutated during model
/// notifications. Notification failures propagate without rolling back committed state.
/// </remarks>
public sealed class MultiSelectionModel<T> : ObservableObject
{
    /// <summary>Identifies the source property.</summary>
    public static readonly DirectProperty<MultiSelectionModel<T>, IReadOnlyObservableList<T>?> SourceProperty =
        Property.RegisterDirect<MultiSelectionModel<T>, IReadOnlyObservableList<T>?>(nameof(Source),
            static model => model.Source, static (model, value) => model.Source = value);

    /// <summary>Identifies the selection mode property.</summary>
    public static readonly DirectProperty<MultiSelectionModel<T>, SelectionMode> SelectionModeProperty =
        Property.RegisterDirect<MultiSelectionModel<T>, SelectionMode>(nameof(SelectionMode),
            static model => model.SelectionMode, static (model, value) => model.SelectionMode = value,
            unsetValue: SelectionMode.Multiple);

    private readonly SelectionResultView<int> _indices = new();
    private readonly SelectionResultView<T> _items = new();
    private IReadOnlyObservableList<T>? _source;
    private SourceSubscription? _sourceSubscription;
    private SelectionMode _selectionMode = SelectionMode.Multiple;
    private bool _notifying;

    /// <summary>Creates an empty model in multiple-selection mode.</summary>
    public MultiSelectionModel()
    {
    }

    /// <summary>Creates a model observing the supplied source.</summary>
    /// <param name="source">The observable source retained by reference.</param>
    public MultiSelectionModel(IReadOnlyObservableList<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Source = source;
    }

    /// <summary>Gets or sets the source; changing its reference clears the selection.</summary>
    public IReadOnlyObservableList<T>? Source
    {
        get => _source;
        set
        {
            VerifyMutationAccess();
            if (ReferenceEquals(_source, value))
                return;
            VerifyNotNotifying();
            var previous = _source;
            _sourceSubscription?.Detach();
            _source = value;
            _sourceSubscription = value is null ? null : new SourceSubscription(this, value);
            _sourceSubscription?.Attach();
            Commit([], () => RaisePropertyChanged(SourceProperty, previous, value));
        }
    }

    /// <summary>Gets the stable, ascending, read-only observable selected indices.</summary>
    public IReadOnlyObservableList<int> SelectedIndices => _indices;

    /// <summary>Gets the stable, read-only observable items corresponding to the selected indices.</summary>
    public IReadOnlyObservableList<T> SelectedItems => _items;

    /// <summary>Gets or sets the permitted selection cardinality.</summary>
    public SelectionMode SelectionMode
    {
        get => _selectionMode;
        set
        {
            VerifyMutationAccess();
            if (_selectionMode == value)
                return;
            VerifyNotNotifying();
            var previous = _selectionMode;
            _selectionMode = value;
            int[] indices = value == SelectionMode.None ? [] :
                value == SelectionMode.Single && _indices.Count > 1 ? [_indices[0]] : _indices.Items;
            Commit(indices, () => RaisePropertyChanged(SelectionModeProperty, previous, value));
        }
    }

    /// <summary>Determines whether an occurrence at the supplied index is selected.</summary>
    /// <param name="index">The zero-based source index.</param>
    public bool IsSelected(int index) => Array.BinarySearch(_indices.Items, index) >= 0;

    /// <summary>Adds an occurrence to the selection, or replaces the selection in single mode.</summary>
    /// <param name="index">The zero-based source index; invalid indices are ignored.</param>
    public void Select(int index)
    {
        VerifyMutationAccess();
        if (!CanSelect(index))
            return;
        if (_selectionMode == SelectionMode.Single)
        {
            Commit([index]);
            return;
        }
        var position = Array.BinarySearch(_indices.Items, index);
        if (position >= 0)
            return;
        position = ~position;
        var result = new int[_indices.Count + 1];
        Array.Copy(_indices.Items, 0, result, 0, position);
        result[position] = index;
        Array.Copy(_indices.Items, position, result, position + 1, _indices.Count - position);
        Commit(result);
    }

    /// <summary>Replaces the selection with one occurrence.</summary>
    /// <param name="index">The zero-based source index; invalid indices are ignored.</param>
    public void SelectOnly(int index)
    {
        VerifyMutationAccess();
        if (CanSelect(index))
            Commit([index]);
    }

    /// <summary>Removes one occurrence from the selection.</summary>
    /// <param name="index">The zero-based source index.</param>
    public void Deselect(int index)
    {
        VerifyMutationAccess();
        var position = Array.BinarySearch(_indices.Items, index);
        if (position < 0)
            return;
        var result = new int[_indices.Count - 1];
        Array.Copy(_indices.Items, 0, result, 0, position);
        Array.Copy(_indices.Items, position + 1, result, position, result.Length - position);
        Commit(result);
    }

    /// <summary>Toggles an occurrence without changing other selected occurrences in multiple mode.</summary>
    /// <param name="index">The zero-based source index.</param>
    public void Toggle(int index)
    {
        if (IsSelected(index))
            Deselect(index);
        else
            Select(index);
    }

    /// <summary>Adds an inclusive range to the selection in multiple mode.</summary>
    /// <param name="start">One inclusive endpoint.</param>
    /// <param name="end">The other inclusive endpoint.</param>
    public void SelectRange(int start, int end) => ChangeRange(start, end, false);

    /// <summary>Replaces the selection with an inclusive range in multiple mode.</summary>
    /// <param name="start">One inclusive endpoint.</param>
    /// <param name="end">The other inclusive endpoint.</param>
    public void SelectRangeOnly(int start, int end) => ChangeRange(start, end, true);

    /// <summary>Selects all current source occurrences in multiple mode.</summary>
    public void SelectAll()
    {
        VerifyMutationAccess();
        if (VerifyMultiple())
            Commit(Enumerable.Range(0, _source?.Count ?? 0).ToArray());
    }

    /// <summary>Clears the selection.</summary>
    public void Clear()
    {
        VerifyMutationAccess();
        Commit([]);
    }

    private bool CanSelect(int index) =>
        _selectionMode != SelectionMode.None && (uint)index < (uint)(_source?.Count ?? 0);

    private bool VerifyMultiple()
    {
        if (_selectionMode == SelectionMode.None)
            return false;
        if (_selectionMode == SelectionMode.Single)
            throw new InvalidOperationException("Range and all-item selection require multiple-selection mode.");
        return true;
    }

    private void ChangeRange(int start, int end, bool replace)
    {
        VerifyMutationAccess();
        if (!VerifyMultiple() || !CanSelect(start) || !CanSelect(end))
            return;
        var low = Math.Min(start, end);
        var high = Math.Max(start, end);
        if (replace)
        {
            Commit(Enumerable.Range(low, high - low + 1).ToArray());
            return;
        }
        var result = new List<int>((int)Math.Min(_source!.Count, (long)_indices.Count + high - low + 1));
        var position = 0;
        while (position < _indices.Count && _indices[position] < low)
            result.Add(_indices[position++]);
        for (var index = low; index <= high; index++)
            result.Add(index);
        while (position < _indices.Count && _indices[position] <= high)
            position++;
        while (position < _indices.Count)
            result.Add(_indices[position++]);
        Commit(result.ToArray());
    }

    private void Commit(int[] indices, Action? propertyNotification = null)
    {
        var items = new T[indices.Length];
        for (var index = 0; index < items.Length; index++)
            items[index] = _source![indices[index]];
        var oldIndices = _indices.Items;
        var oldItems = _items.Items;
        var indicesChanged = !oldIndices.AsSpan().SequenceEqual(indices);
        var itemsChanged = !SameItems(oldItems, items);
        if (!indicesChanged && !itemsChanged && propertyNotification is null)
            return;
        VerifyNotNotifying();
        _indices.Commit(indices);
        _items.Commit(items);
        _notifying = true;
        try
        {
            propertyNotification?.Invoke();
            if (indicesChanged)
                _indices.Publish(oldIndices);
            if (itemsChanged)
                _items.Publish(oldItems);
        }
        finally
        {
            _notifying = false;
        }
    }

    private static bool SameItems(T[] left, T[] right)
    {
        if (left.Length != right.Length)
            return false;
        for (var index = 0; index < left.Length; index++)
        {
            if (typeof(T).IsValueType || typeof(T) == typeof(string))
            {
                if (!EqualityComparer<T>.Default.Equals(left[index], right[index]))
                    return false;
            }
            else if (!ReferenceEquals(left[index], right[index]))
                return false;
        }
        return true;
    }

    private void VerifyNotNotifying()
    {
        if (_notifying)
            throw new InvalidOperationException("Selection cannot change during notification.");
    }

    private void OnSourceChanged(ListChangedEventArgs<T> change)
    {
        VerifyMutationAccess();
        VerifyNotNotifying();
        if (_indices.Count == 0)
            return;
        var indices = new List<int>(_indices.Count);
        foreach (var index in _indices.Items)
        {
            var next = MapIndex(index, change);
            if (next >= 0)
                indices.Add(next);
        }
        if (change.Kind == ListChangeKind.Reorder)
            indices.Sort();
        Commit(indices.ToArray());
    }

    private static int MapIndex(int index, ListChangedEventArgs<T> change)
    {
        if (change.Kind == ListChangeKind.Reorder)
            return change.Permutation[index];
        if (index < change.Index)
            return index;
        var removed = change.OldItems.Count;
        if (index - change.Index < removed)
            return -1;
        return index + change.NewItems.Count - removed;
    }

    private sealed class SourceSubscription(MultiSelectionModel<T> owner, IReadOnlyObservableList<T> source)
    {
        private readonly WeakReference<MultiSelectionModel<T>> _owner = new(owner);

        internal void Attach() => source.Changed += OnChanged;
        internal void Detach() => source.Changed -= OnChanged;

        private void OnChanged(object? sender, ListChangedEventArgs<T> change)
        {
            if (!_owner.TryGetTarget(out var owner))
            {
                Detach();
                return;
            }
            if (ReferenceEquals(owner._sourceSubscription, this))
                owner.OnSourceChanged(change);
        }
    }
}

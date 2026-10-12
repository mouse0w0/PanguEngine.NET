using PanguEngine.Collections;
using PanguEngine.ComponentModel;

namespace PanguEngine.Client.UI.Selection;

public sealed class SingleSelectionModel<T> : ObservableObject
{
    public static readonly DirectProperty<SingleSelectionModel<T>, IReadOnlyObservableList<T>?> SourceProperty =
        Property.RegisterDirect<SingleSelectionModel<T>, IReadOnlyObservableList<T>?>(
            nameof(Source),
            static owner => owner.Source,
            static (owner, value) => owner.Source = value,
            onChanged: static (owner, oldSource, _) => owner.OnSourceChanged(oldSource));

    public static readonly DirectProperty<SingleSelectionModel<T>, int> SelectedIndexProperty =
        Property.RegisterDirect<SingleSelectionModel<T>, int>(
            nameof(SelectedIndex),
            static owner => owner.SelectedIndex,
            static (owner, value) => owner.SelectedIndex = value,
            unsetValue: -1,
            onChanged: static (owner, oldIndex, _) => owner.OnIndexChanged(oldIndex));

    public static readonly DirectProperty<SingleSelectionModel<T>, T?> SelectedItemProperty =
        Property.RegisterDirect<SingleSelectionModel<T>, T?>(
            nameof(SelectedItem),
            static owner => owner.SelectedItem,
            static (owner, value) => owner.SelectedItem = value);

    private int _selectedIndex = -1;
    private bool _notifying;
    private IReadOnlyObservableList<T>? _source;
    private SourceSubscription? _sourceSubscription;

    public SingleSelectionModel()
    {
    }

    public SingleSelectionModel(IReadOnlyObservableList<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Source = source;
    }

    public IReadOnlyObservableList<T>? Source
    {
        get => _source;
        set
        {
            if (_notifying && !ReferenceEquals(_source, value))
                VerifyNotNotifying();
            SetField(SourceProperty, ref _source, value);
        }
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            var index = (uint)value < (uint)(_source?.Count ?? 0) ? value : -1;
            if (_notifying && index != _selectedIndex)
                VerifyNotNotifying();
            SetField(SelectedIndexProperty, ref _selectedIndex, index);
        }
    }

    public T? SelectedItem
    {
        get => ReadItem();
        set
        {
            if (_notifying && SameItem(ReadItem(), value))
                return;
            SelectedIndex = FindItemIndex(value);
        }
    }

    public void Select(int index)
    {
        VerifyMutationAccess();
        if ((uint)index < (uint)(_source?.Count ?? 0))
            SelectedIndex = index;
    }

    public void Clear() => SelectedIndex = -1;

    private void OnSourceChanged(IReadOnlyObservableList<T>? oldSource)
    {
        var oldIndex = _selectedIndex;
        var oldItem = oldIndex < 0 ? default : oldSource![oldIndex];
        _sourceSubscription?.Detach();
        _sourceSubscription = null;
        _selectedIndex = -1;
        if (_source is not null)
        {
            var subscription = new SourceSubscription(this, _source);
            _sourceSubscription = subscription;
            subscription.Attach();
        }

        PublishSelection(oldIndex, oldItem);
    }

    private T? ReadItem() => _selectedIndex < 0 ? default : _source![_selectedIndex];

    private void OnIndexChanged(int oldIndex)
    {
        if (_notifying)
            return;
        var oldItem = oldIndex < 0 ? default : _source![oldIndex];
        PublishSelection(oldIndex, oldItem, false);
    }

    private int FindItemIndex(T? item)
    {
        for (var index = 0; index < (_source?.Count ?? 0); index++)
        {
            if (EqualityComparer<T?>.Default.Equals(_source![index], item))
            {
                return index;
            }
        }

        return -1;
    }

    private void Commit(int index, T? oldItem)
    {
        var oldIndex = _selectedIndex;
        _selectedIndex = index;
        PublishSelection(oldIndex, oldItem);
    }

    private void PublishSelection(int oldIndex, T? oldItem, bool notifyIndex = true)
    {
        var newIndex = _selectedIndex;
        var newItem = ReadItem();
        _notifying = true;
        try
        {
            if (notifyIndex && oldIndex != newIndex)
                RaisePropertyChanged(SelectedIndexProperty, oldIndex, newIndex);
            if (!SameItem(oldItem, newItem))
                RaisePropertyChanged(SelectedItemProperty, oldItem, newItem);
        }
        finally
        {
            _notifying = false;
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(PropertyChangedEventArgs eventArgs)
    {
        var wasNotifying = _notifying;
        _notifying = true;
        try
        {
            base.OnPropertyChanged(eventArgs);
        }
        finally
        {
            _notifying = wasNotifying;
        }
    }

    private void OnItemsChanged(ListChangedEventArgs<T> change)
    {
        VerifyMutationAccess();
        VerifyNotNotifying();
        if (_selectedIndex < 0)
            return;

        var oldIndex = _selectedIndex;
        var newIndex = oldIndex;
        T? oldItem;
        switch (change.Kind)
        {
            case ListChangeKind.Add:
                if (change.Index <= oldIndex)
                    newIndex += change.NewItems.Count;
                oldItem = _source![newIndex];
                break;

            case ListChangeKind.Remove:
            case ListChangeKind.Replace:
                var offset = oldIndex - change.Index;
                if ((uint)offset < (uint)change.OldItems.Count)
                {
                    oldItem = change.OldItems[offset];
                    newIndex = -1;
                }
                else
                {
                    if (offset >= change.OldItems.Count)
                        newIndex += change.NewItems.Count - change.OldItems.Count;
                    oldItem = _source![newIndex];
                }
                break;

            case ListChangeKind.Reorder:
                newIndex = change.Permutation[oldIndex];
                oldItem = _source![newIndex];
                break;

            default:
                return;
        }

        Commit(newIndex, oldItem);
    }

    private void VerifyNotNotifying()
    {
        if (_notifying)
            throw new InvalidOperationException("Selection cannot change during notification.");
    }

    private static bool SameItem(T? left, T? right) => SelectedItemProperty.ValuesEqual(left, right);

    private sealed class SourceSubscription(SingleSelectionModel<T> owner, IReadOnlyObservableList<T> source)
    {
        private readonly WeakReference<SingleSelectionModel<T>> _owner = new(owner);

        internal void Attach() => source.Changed += OnChanged;

        internal void Detach() => source.Changed -= OnChanged;

        private void OnChanged(object? sender, ListChangedEventArgs<T> change)
        {
            if (!_owner.TryGetTarget(out var model))
            {
                Detach();
                return;
            }

            if (ReferenceEquals(model._sourceSubscription, this))
                model.OnItemsChanged(change);
        }
    }
}

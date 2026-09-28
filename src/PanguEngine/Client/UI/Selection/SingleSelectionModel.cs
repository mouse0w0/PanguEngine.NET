using PanguEngine.Collections;
using PanguEngine.ComponentModel;

namespace PanguEngine.Client.UI.Selection;

public sealed class SingleSelectionModel<T> : ObservableObject
{
    public static readonly DirectProperty<SingleSelectionModel<T>, IReadOnlyObservableList<T>?> SourceProperty =
        Property.RegisterDirect<SingleSelectionModel<T>, IReadOnlyObservableList<T>?>(
            nameof(Source),
            static owner => owner._source,
            static (owner, value) => owner.SetSource(value));

    public static readonly DirectProperty<SingleSelectionModel<T>, int> SelectedIndexProperty =
        Property.RegisterDirect<SingleSelectionModel<T>, int>(
            nameof(SelectedIndex),
            static owner => owner._selectedIndex,
            static (owner, value) => owner.SetIndex(value),
            unsetValue: -1);

    public static readonly DirectProperty<SingleSelectionModel<T>, T?> SelectedItemProperty =
        Property.RegisterDirect<SingleSelectionModel<T>, T?>(
            nameof(SelectedItem),
            static owner => owner.ReadItem(),
            static (owner, value) => owner.SetItem(value));

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
        SetSource(source);
    }

    public IReadOnlyObservableList<T>? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public T? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public void Select(int index)
    {
        VerifyMutationAccess();
        if ((uint)index < (uint)(_source?.Count ?? 0))
            SetIndex(index);
    }

    public void Clear() => SetIndex(-1);

    private void SetSource(IReadOnlyObservableList<T>? source)
    {
        VerifyMutationAccess();
        if (ReferenceEquals(_source, source))
            return;
        VerifyNotNotifying();
        var oldSource = _source;
        var oldIndex = _selectedIndex;
        var oldItem = ReadItem();
        _sourceSubscription?.Detach();
        _sourceSubscription = null;
        _source = source;
        _selectedIndex = -1;
        if (source is not null)
        {
            var subscription = new SourceSubscription(this, source);
            _sourceSubscription = subscription;
            subscription.Attach();
        }

        Publish(oldSource, oldIndex, oldItem);
    }

    private T? ReadItem() => _selectedIndex < 0 ? default : _source![_selectedIndex];

    private void SetIndex(int index)
    {
        VerifyMutationAccess();
        var newIndex = (uint)index < (uint)(_source?.Count ?? 0) ? index : -1;
        if (newIndex == _selectedIndex)
            return;
        VerifyNotNotifying();
        Commit(newIndex, ReadItem());
    }

    private void SetItem(T? item)
    {
        VerifyMutationAccess();
        if (_notifying && SameItem(ReadItem(), item))
            return;

        for (var index = 0; index < (_source?.Count ?? 0); index++)
        {
            if (EqualityComparer<T?>.Default.Equals(_source![index], item))
            {
                SetIndex(index);
                return;
            }
        }

        SetIndex(-1);
    }

    private void Commit(int index, T? oldItem)
    {
        var oldIndex = _selectedIndex;
        _selectedIndex = index;
        Publish(_source, oldIndex, oldItem);
    }

    private void Publish(IReadOnlyObservableList<T>? oldSource, int oldIndex, T? oldItem)
    {
        var newIndex = _selectedIndex;
        var newItem = ReadItem();
        _notifying = true;
        try
        {
            if (!ReferenceEquals(oldSource, _source))
                RaisePropertyChanged(SourceProperty, oldSource, _source);
            if (oldIndex != newIndex)
                RaisePropertyChanged(SelectedIndexProperty, oldIndex, newIndex);
            if (!SameItem(oldItem, newItem))
                RaisePropertyChanged(SelectedItemProperty, oldItem, newItem);
        }
        finally
        {
            _notifying = false;
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

    private static bool SameItem(T? left, T? right) => typeof(T).IsValueType
        ? EqualityComparer<T?>.Default.Equals(left, right)
        : ReferenceEquals(left, right);

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

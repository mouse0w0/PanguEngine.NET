using PanguEngine.Collections;
using PanguEngine.ComponentModel;
using PanguEngine.UI.Controls;

namespace PanguEngine.UI.Selection;

/// <summary>Tracks reference-identified selections independently of tree visibility.</summary>
/// <typeparam name="T">The business node type.</typeparam>
/// <remarks>Use on the owning tree's scene thread and do not mutate the tree or selection during notifications.</remarks>
public sealed class TreeSelectionModel<T> : ObservableObject where T : class
{
    /// <summary>Identifies the permitted selection cardinality.</summary>
    public static readonly DirectProperty<TreeSelectionModel<T>, SelectionMode> SelectionModeProperty =
        Property.RegisterDirect<TreeSelectionModel<T>, SelectionMode>(nameof(SelectionMode),
            static model => model.SelectionMode, static (model, value) => model.SelectionMode = value,
            unsetValue: SelectionMode.Single);

    private readonly IReadOnlyDictionary<T, TreeView<T>.NodeState> _known;
    private readonly SelectionResultView<T> _items = new();
    private readonly HashSet<T> _selected = new(ReferenceEqualityComparer.Instance);
    private SelectionMode _mode = SelectionMode.Single;

    internal TreeSelectionModel(IReadOnlyDictionary<T, TreeView<T>.NodeState> known) => _known = known;

    /// <summary>Gets the stable selection results in insertion order.</summary>
    public IReadOnlyObservableList<T> SelectedItems => _items;

    /// <summary>Gets or sets the permitted selection cardinality.</summary>
    public SelectionMode SelectionMode
    {
        get => _mode;
        set
        {
            if (_mode == value)
                return;
            var old = _mode;
            _mode = value;
            T[] result = value == SelectionMode.None ? [] :
                value == SelectionMode.Single && _items.Count > 1 ? [_items[0]] : _items.Items;
            var previous = Commit(result);
            RaisePropertyChanged(SelectionModeProperty, old, value);
            Publish(previous);
        }
    }

    /// <summary>Determines whether the supplied node is selected.</summary>
    public bool IsSelected(T node) => _selected.Contains(node);

    /// <summary>Selects a known node according to the current mode.</summary>
    public void Select(T node)
    {
        if (_mode == SelectionMode.None || !_known.ContainsKey(node) || IsSelected(node))
            return;
        Change(_mode == SelectionMode.Single ? [node] : [.. _items.Items, node]);
    }

    /// <summary>Replaces the selection with one known node.</summary>
    public void SelectOnly(T node)
    {
        if (_mode != SelectionMode.None && _known.ContainsKey(node))
            Change([node]);
    }

    /// <summary>Removes a node from the selection.</summary>
    public void Deselect(T node)
    {
        if (IsSelected(node))
            Change(_items.Items.Where(item => !ReferenceEquals(item, node)).ToArray());
    }

    /// <summary>Toggles a known node's selection.</summary>
    public void Toggle(T node)
    {
        if (IsSelected(node))
            Deselect(node);
        else
            Select(node);
    }

    /// <summary>Clears the selection.</summary>
    public void Clear() => Change([]);

    internal void Choose(IReadOnlyList<T> candidates, bool append)
    {
        if (_mode != SelectionMode.Multiple)
            return;
        var wanted = new HashSet<T>(candidates, ReferenceEqualityComparer.Instance);
        var result = new List<T>();
        foreach (var old in _items.Items)
            if (append || wanted.Contains(old))
                result.Add(old);
        foreach (var item in candidates)
            if (_known.ContainsKey(item) && !_selected.Contains(item))
                result.Add(item);
        Change(result.ToArray());
    }

    internal T[]? Prune() => Commit(_items.Items.Where(_known.ContainsKey).ToArray());

    internal void Publish(T[]? previous)
    {
        if (previous is not null)
            _items.Publish(previous);
    }

    private void Change(T[] result) => Publish(Commit(result));

    private T[]? Commit(T[] result)
    {
        if (_items.Count == result.Length && _items.Items.AsSpan().SequenceEqual(result, ReferenceEqualityComparer.Instance))
            return null;
        var previous = _items.Items;
        _selected.Clear();
        foreach (var item in result)
            _selected.Add(item);
        _items.Commit(result);
        return previous;
    }
}

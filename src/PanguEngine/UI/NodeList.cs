using PanguEngine.Collections;

namespace PanguEngine.UI;

/// <summary>
/// Provides observable, validated mutable access to the direct children of a UI parent.
/// </summary>
/// <remarks>
/// Changes are published after the corresponding tree update. Other collections notify synchronously
/// during their own updates. A collection involved in an active update cannot be modified recursively.
/// Sorting requires comparable nodes or an explicit comparer.
/// </remarks>
public sealed partial class NodeList : ObservableList<Node>
{
    private readonly Parent _owner;

    internal NodeList(Parent owner) => _owner = owner;

    /// <inheritdoc />
    protected override int IndexOfItem(Node? item)
    {
        for (var index = 0; index < Count; index++)
        {
            if (ReferenceEquals(this[index], item))
                return index;
        }
        return -1;
    }

    /// <inheritdoc />
    protected override void SetItem(int index, Node value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (ReferenceEquals(this[index], value))
            return;
        ReplaceChildren(index, 1, [value], () => base.SetItem(index, value));
    }

    /// <inheritdoc />
    protected override void InsertItem(int index, Node item) =>
        ReplaceChildren(index, 0, [item], () => base.InsertItem(index, item));

    /// <inheritdoc />
    protected override void RemoveItem(int index) =>
        ReplaceChildren(index, 1, [], () => base.RemoveItem(index));

    /// <inheritdoc />
    protected override void ClearItems() =>
        ReplaceChildren(0, Count, [], () => base.ClearItems());

    /// <inheritdoc />
    protected override void InsertItems(int index, IReadOnlyList<Node> items) =>
        ReplaceChildren(index, 0, items, () => base.InsertItems(index, items));

    /// <inheritdoc />
    protected override void RemoveItems(int index, int count) =>
        ReplaceChildren(index, count, [], () => base.RemoveItems(index, count));

    /// <inheritdoc />
    protected override void ReplaceItems(IReadOnlyList<Node> items) =>
        ReplaceChildren(0, Count, items, () => base.ReplaceItems(items));

    /// <inheritdoc />
    protected override void MoveItem(int oldIndex, int newIndex)
    {
        VerifyTreeAccess();
        ReorderChildren(() => base.MoveItem(oldIndex, newIndex));
    }

    /// <inheritdoc />
    protected override void ReorderItems(IReadOnlyList<int> permutation)
    {
        VerifyTreeAccess();
        ReorderChildren(() => base.ReorderItems(permutation));
    }
}

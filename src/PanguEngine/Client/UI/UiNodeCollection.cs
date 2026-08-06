using System.Collections;

namespace PanguEngine.Client.UI;

/// <summary>
/// Provides validated mutable access to the direct children of a UI panel.
/// </summary>
public sealed class UiNodeCollection : IList<UiNode>, IReadOnlyList<UiNode>
{
    private readonly Parent _owner;
    private readonly IReadOnlyList<UiNode> _items;

    internal UiNodeCollection(Parent owner)
    {
        _owner = owner;
        _items = owner.Children;
    }

    /// <summary>
    /// Gets or replaces the child at an index.
    /// </summary>
    /// <param name="index">The zero-based child index.</param>
    /// <returns>The child at the requested index.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a replacement value is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is invalid.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when replacement would violate the tree or open screen owner-thread contract.
    /// </exception>
    public UiNode this[int index]
    {
        get => _items[index];
        set => _owner.ReplaceChildFromCollection(index, value);
    }

    /// <summary>
    /// Gets the number of direct children.
    /// </summary>
    public int Count => _items.Count;

    /// <summary>
    /// Gets whether the collection is read-only.
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// Adds a child after all existing children.
    /// </summary>
    /// <param name="item">The child to add.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the operation would violate the tree or open screen owner-thread contract.
    /// </exception>
    public void Add(UiNode item) =>
        _owner.AddChildFromCollection(item);

    /// <summary>
    /// Removes all direct children.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when a tree owned by an open screen is modified from the wrong thread.</exception>
    public void Clear() =>
        _owner.ClearChildrenFromCollection();

    /// <summary>
    /// Gets whether the collection contains a child.
    /// </summary>
    /// <param name="item">The child to locate.</param>
    /// <returns>Whether the child is present.</returns>
    public bool Contains(UiNode? item) =>
        IndexOf(item) >= 0;

    /// <summary>
    /// Copies the children to an array.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The starting destination index.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="array"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="arrayIndex"/> is invalid.</exception>
    /// <exception cref="ArgumentException">Thrown when the destination array is too small.</exception>
    public void CopyTo(UiNode[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);
        if ((uint)arrayIndex > (uint)array.Length)
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        if (array.Length - arrayIndex < Count)
            throw new ArgumentException("The destination array does not have enough space.", nameof(array));

        for (var index = 0; index < Count; index++)
            array[arrayIndex + index] = _items[index];
    }

    /// <summary>
    /// Finds the index of a child.
    /// </summary>
    /// <param name="item">The child to locate.</param>
    /// <returns>The child index, or -1 when it is absent.</returns>
    public int IndexOf(UiNode? item)
    {
        if (item is null)
            return -1;

        for (var index = 0; index < Count; index++)
        {
            if (EqualityComparer<UiNode>.Default.Equals(_items[index], item))
                return index;
        }

        return -1;
    }

    /// <summary>
    /// Inserts a child at an index.
    /// </summary>
    /// <param name="index">The zero-based insertion index.</param>
    /// <param name="item">The child to insert.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is invalid.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the operation would violate the tree or open screen owner-thread contract.
    /// </exception>
    public void Insert(int index, UiNode item) =>
        _owner.InsertChildFromCollection(index, item);

    /// <summary>
    /// Removes a direct child.
    /// </summary>
    /// <param name="item">The child to remove.</param>
    /// <returns>Whether the child was present and removed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a tree owned by an open screen is modified from the wrong thread.</exception>
    public bool Remove(UiNode item) =>
        _owner.RemoveChildFromCollection(item);

    /// <summary>
    /// Removes the child at an index.
    /// </summary>
    /// <param name="index">The zero-based child index.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a tree owned by an open screen is modified from the wrong thread.</exception>
    public void RemoveAt(int index) =>
        _owner.RemoveChildAtFromCollection(index);

    /// <summary>
    /// Moves a child to a final index without changing its parent.
    /// </summary>
    /// <param name="oldIndex">The current zero-based child index.</param>
    /// <param name="newIndex">The zero-based child index after the move.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either index is invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a tree owned by an open screen is modified from the wrong thread.</exception>
    public void Move(int oldIndex, int newIndex) =>
        _owner.MoveChildFromCollection(oldIndex, newIndex);

    /// <summary>
    /// Returns an enumerator over the children in drawing order.
    /// </summary>
    /// <returns>An enumerator over the direct children.</returns>
    public IEnumerator<UiNode> GetEnumerator() =>
        _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() =>
        GetEnumerator();
}

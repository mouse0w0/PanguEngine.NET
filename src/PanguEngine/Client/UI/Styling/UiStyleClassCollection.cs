using System.Collections;

namespace PanguEngine.Client.UI.Styling;

/// <summary>
/// Provides a reference-stable collection of style classes owned by a node.
/// </summary>
/// <remarks>
/// Membership uses Ordinal comparison, invalid identifiers are rejected, and duplicate additions are ignored.
/// The owning node exposes this collection through a read-only reference; the reference cannot be replaced, although the contents may change.
/// </remarks>
public sealed class UiStyleClassCollection : ICollection<string>, IReadOnlySet<string>
{
    private readonly UiNode _owner;
    private readonly List<string> _items = new();

    internal UiStyleClassCollection(UiNode owner) => _owner = owner;

    /// <summary>Adds a class when it is a valid identifier and not already present.</summary>
    /// <param name="item">The class to add.</param>
    /// <returns>true when the class was added and triggered a style recompute; false when it was already present.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="item"/> is not a valid ASCII identifier.</exception>
    public bool Add(string item)
    {
        ArgumentNullException.ThrowIfNull(item);
        UiStyleIdentifier.ThrowIfInvalid(item, nameof(item));
        if (_items.Contains(item, StringComparer.Ordinal))
            return false;
        _owner.VerifyStyleInputAccess();
        _owner.ChangeStyleInput(
            () => _items.Add(item),
            () => _items.RemoveAt(_items.Count - 1));
        return true;
    }

    /// <inheritdoc />
    void ICollection<string>.Add(string item) => Add(item);

    /// <inheritdoc />
    public void Clear()
    {
        if (_items.Count == 0)
            return;
        _owner.VerifyStyleInputAccess();
        var previous = _items.ToArray();
        _owner.ChangeStyleInput(
            _items.Clear,
            () => _items.AddRange(previous));
    }

    /// <inheritdoc cref="ICollection{string}.Contains" />
    public bool Contains(string item) => _items.Contains(item, StringComparer.Ordinal);

    /// <inheritdoc />
    public void CopyTo(string[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

    /// <inheritdoc />
    public bool Remove(string item)
    {
        if (!_items.Contains(item, StringComparer.Ordinal))
            return false;
        _owner.VerifyStyleInputAccess();
        var index = _items.IndexOf(item);
        _owner.ChangeStyleInput(
            () => _items.RemoveAt(index),
            () => _items.Insert(index, item));
        return true;
    }

    /// <inheritdoc />
    public bool IsProperSubsetOf(IEnumerable<string> other) => CreateSet().IsProperSubsetOf(other);

    /// <inheritdoc />
    public bool IsProperSupersetOf(IEnumerable<string> other) => CreateSet().IsProperSupersetOf(other);

    /// <inheritdoc />
    public bool IsSubsetOf(IEnumerable<string> other) => CreateSet().IsSubsetOf(other);

    /// <inheritdoc />
    public bool IsSupersetOf(IEnumerable<string> other) => CreateSet().IsSupersetOf(other);

    /// <inheritdoc />
    public bool Overlaps(IEnumerable<string> other) => CreateSet().Overlaps(other);

    /// <inheritdoc />
    public bool SetEquals(IEnumerable<string> other) => CreateSet().SetEquals(other);

    /// <inheritdoc cref="ICollection{string}.Count" />
    public int Count => _items.Count;

    /// <inheritdoc />
    public bool IsReadOnly => false;

    /// <inheritdoc />
    public IEnumerator<string> GetEnumerator() => _items.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private HashSet<string> CreateSet() => new(_items, StringComparer.Ordinal);
}

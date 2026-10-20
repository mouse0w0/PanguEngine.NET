namespace PanguEngine.World.Entities;

internal interface IComponentColumn
{
    void EnsureCapacity(int capacity);

    void SetBoxed(int index, object value);

    void RemoveAtSwapBack(int index, int lastIndex);
}

internal sealed class ComponentColumn<T> : IComponentColumn where T : unmanaged
{
    private T[] _values = [];

    internal T Get(int index) => _values[index];

    internal void Set(int index, T value) => _values[index] = value;

    internal Span<T> AsSpan(int count) => _values.AsSpan(0, count);

    public void EnsureCapacity(int capacity)
    {
        if (_values.Length >= capacity)
            return;

        var newCapacity = Math.Max(capacity, Math.Max(4, _values.Length * 2));
        Array.Resize(ref _values, newCapacity);
    }

    public void SetBoxed(int index, object value)
    {
        _values[index] = (T)value;
    }

    public void RemoveAtSwapBack(int index, int lastIndex)
    {
        if (index != lastIndex)
            _values[index] = _values[lastIndex];
        _values[lastIndex] = default;
    }
}

internal sealed class Archetype
{
    private readonly IReadOnlyList<ComponentType> _signature;
    private readonly Dictionary<ComponentType, IComponentColumn> _columns =
        new(ReferenceEqualityComparer.Instance);
    private EntityHandle[] _handles = [];

    internal Archetype(IReadOnlyList<ComponentType> signature)
    {
        _signature = signature;
        foreach (var componentType in signature)
            _columns.Add(componentType, componentType.CreateColumn());
    }

    internal int Count { get; private set; }

    internal ReadOnlySpan<EntityHandle> Handles => _handles.AsSpan(0, Count);

    internal int Add(EntityHandle handle, IReadOnlyDictionary<ComponentType, object> values)
    {
        var row = Count;
        EnsureCapacity(row + 1);
        _handles[row] = handle;
        foreach (var componentType in _signature)
            _columns[componentType].SetBoxed(row, values[componentType]);
        Count++;
        return row;
    }

    internal EntityHandle? RemoveAtSwapBack(int row)
    {
        var lastRow = Count - 1;
        EntityHandle? movedHandle = row == lastRow ? null : _handles[lastRow];
        if (row != lastRow)
            _handles[row] = _handles[lastRow];
        _handles[lastRow] = default;

        foreach (var column in _columns.Values)
            column.RemoveAtSwapBack(row, lastRow);

        Count--;
        return movedHandle;
    }

    internal T Get<T>(int row, ComponentType<T> type) where T : unmanaged =>
        GetColumn(type).Get(row);

    internal void Set<T>(int row, ComponentType<T> type, T value) where T : unmanaged =>
        GetColumn(type).Set(row, value);

    internal Span<T> GetSpan<T>(ComponentType<T> type) where T : unmanaged =>
        GetColumn(type).AsSpan(Count);

    private ComponentColumn<T> GetColumn<T>(ComponentType<T> type) where T : unmanaged
    {
        return _columns.TryGetValue(type, out var column)
            ? (ComponentColumn<T>)column
            : throw new InvalidOperationException("The archetype does not contain the component type.");
    }

    private void EnsureCapacity(int capacity)
    {
        if (_handles.Length < capacity)
        {
            var newCapacity = Math.Max(capacity, Math.Max(4, _handles.Length * 2));
            Array.Resize(ref _handles, newCapacity);
        }

        foreach (var column in _columns.Values)
            column.EnsureCapacity(capacity);
    }
}

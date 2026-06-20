namespace PanguEngine.Events;

internal sealed class EventListenerList
{
    private static readonly Order[] Orders = Enum.GetValues<Order>();

    private readonly EventListenerList? _parent;
    private readonly List<EventListenerList> _children = [];
    private readonly List<EventListener>?[] _buckets = new List<EventListener>?[Orders.Length];
    private EventListener[] _snapshot = [];
    private bool _dirty = true;

    internal EventListenerList(EventListenerList? parent = null)
    {
        _parent = parent;
        _parent?.AddChild(this);
    }

    internal IReadOnlyList<EventListener> Listeners => EnsureSnapshot();

    internal void AddChild(EventListenerList child)
    {
        _children.Add(child);
    }

    internal void AddListener(EventListener listener)
    {
        var bucketIndex = (int)listener.Order;
        _buckets[bucketIndex] ??= [];
        _buckets[bucketIndex]!.Add(listener);
        MarkDirty();
    }

    internal void RemoveListener(EventListener listener)
    {
        var bucket = _buckets[(int)listener.Order];
        if (bucket is null || !bucket.Remove(listener))
            return;

        MarkDirty();
    }

    private EventListener[] EnsureSnapshot()
    {
        if (_dirty)
            RebuildSnapshot();

        return _snapshot;
    }

    private void RebuildSnapshot()
    {
        var listeners = new List<EventListener>();
        foreach (var order in Orders)
            CollectListeners(order, listeners);

        _snapshot = listeners.ToArray();
        _dirty = false;
    }

    private void CollectListeners(Order order, List<EventListener> listeners)
    {
        for (var node = this; node is not null; node = node._parent)
        {
            var bucket = node._buckets[(int)order];
            if (bucket is not null)
                listeners.AddRange(bucket);
        }
    }

    private void MarkDirty()
    {
        _dirty = true;
        foreach (var child in _children)
            child.MarkDirty();
    }
}
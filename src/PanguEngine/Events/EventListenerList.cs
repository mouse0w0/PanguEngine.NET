namespace PanguEngine.Events;

internal sealed class EventListenerList
{
    private readonly HashSet<EventListenerList> _children = new(ReferenceEqualityComparer.Instance);
    private readonly List<EventListener> _localListeners = [];
    private readonly List<EventListener> _listeners = [];

    internal IReadOnlyList<EventListener> Listeners => _listeners;

    internal void AddChild(EventListenerList child)
    {
        if (_children.Add(child))
        {
            foreach (var listener in _localListeners)
                child.InsertLocalListener(listener);
        }
    }

    internal void AddListener(EventListener listener)
    {
        _localListeners.Add(listener);
        InsertLocalListener(listener);
        foreach (var child in _children)
            child.InsertLocalListener(listener);
    }

    internal void RemoveListener(EventListener listener)
    {
        _localListeners.Remove(listener);
        RemoveLocalListener(listener);
        foreach (var child in _children)
            child.RemoveLocalListener(listener);
    }

    private void InsertLocalListener(EventListener listener)
    {
        var left = 0;
        var right = _listeners.Count;
        while (left < right)
        {
            var middle = (left + right) / 2;
            if (CompareListeners(listener, _listeners[middle]) < 0)
                right = middle;
            else
                left = middle + 1;
        }

        _listeners.Insert(left, listener);
    }

    private void RemoveLocalListener(EventListener listener)
    {
        _listeners.Remove(listener);
    }

    private static int CompareListeners(EventListener left, EventListener right)
    {
        return left.Order.CompareTo(right.Order);
    }
}
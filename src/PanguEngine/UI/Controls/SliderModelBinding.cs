using System.Diagnostics.CodeAnalysis;

namespace PanguEngine.UI.Controls;

internal sealed class SliderModelBinding<T>
{
    private readonly List<Connection> _connections = [];

    internal Connection Connect(Slider<T> target)
    {
        var connection = new Connection(this, target);
        _connections.Add(connection);
        return connection;
    }

    internal void VerifyAccess()
    {
        foreach (var connection in _connections.ToArray())
            if (connection.TryGetTarget(out var target))
                target.VerifyModelAccess();
    }

    internal void Update(SliderValueModelChange<T> change)
    {
        var changes = new List<(Slider<T> Target, SliderSelectionChange<T> Change)>();
        foreach (var connection in _connections.ToArray())
            if (connection.TryGetTarget(out var target))
                changes.Add((target, target.PrepareModelChange(change)));
        foreach (var (target, selectionChange) in changes)
            target.PublishModelChange(selectionChange);
    }

    internal sealed class Connection(SliderModelBinding<T> binding, Slider<T> target)
    {
        private readonly WeakReference<Slider<T>> _target = new(target);
        private bool _isAttached = true;

        internal void Detach()
        {
            _isAttached = false;
            binding._connections.Remove(this);
        }

        internal bool TryGetTarget([NotNullWhen(true)] out Slider<T>? target)
        {
            if (_isAttached && _target.TryGetTarget(out target))
                return true;
            Detach();
            target = null;
            return false;
        }
    }
}

internal readonly record struct SliderSelectionChange<T>(
    SliderValueState<T> OldState,
    SliderValueState<T> State,
    (int State, int Availability) Version);

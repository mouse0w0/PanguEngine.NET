namespace PanguEngine.World.Entities;

/// <summary>
/// Collects the required component values used to create one entity.
/// </summary>
public sealed class EntitySpawnBuilder
{
    private readonly Dictionary<ComponentType, object> _values = new(ReferenceEqualityComparer.Instance);
    private bool _isConsumed;

    /// <summary>
    /// Adds one required component value.
    /// </summary>
    /// <typeparam name="T">The component value type.</typeparam>
    /// <param name="type">The component metadata.</param>
    /// <param name="value">The initial component value.</param>
    /// <returns>This builder.</returns>
    public EntitySpawnBuilder Add<T>(ComponentType<T> type, T value) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(type);
        EnsureAvailable();
        if (!_values.TryAdd(type, value))
            throw new InvalidOperationException("A value for the component type has already been added.");

        return this;
    }

    internal IReadOnlyDictionary<ComponentType, object> Consume()
    {
        EnsureAvailable();
        _isConsumed = true;
        return _values;
    }

    private void EnsureAvailable()
    {
        if (_isConsumed)
            throw new InvalidOperationException("The entity spawn builder has already been consumed.");
    }
}

namespace PanguEngine.Registries;

/// <summary>
/// Stores values by resource key and falls back to a default value after freezing.
/// </summary>
/// <typeparam name="T">The registered value type.</typeparam>
public class DefaultedRegistry<T> : Registry<T> where T : class
{
    private RegistryEntry<T>? _defaultEntry;

    /// <summary>
    /// Creates a defaulted registry with the specified key.
    /// </summary>
    /// <param name="key">The key that identifies this registry.</param>
    public DefaultedRegistry(ResourceKey key) : base(key)
    {
    }

    /// <summary>The key of the default entry.</summary>
    public ResourceKey? DefaultKey { get; private set; }

    /// <summary>The registry-local identifier of the default entry, or -1 when no default entry is cached.</summary>
    public int DefaultId => _defaultEntry?.Id ?? -1;

    /// <summary>The cached default value.</summary>
    public T? DefaultValue => _defaultEntry?.Value;

    /// <summary>
    /// Sets the key that will be resolved as the default entry when the registry is frozen.
    /// </summary>
    /// <param name="key">The key of the default entry.</param>
    public void SetDefault(ResourceKey key)
    {
        if (IsFrozen)
            throw new InvalidOperationException("Registry is frozen.");
        if (!ResourceKey.IsValid(key))
            throw new ArgumentException("Invalid registry key.", nameof(key));

        DefaultKey = key;
        _defaultEntry = null;
    }

    /// <inheritdoc/>
    public override T Get(ResourceKey key)
    {
        if (TryGet(key, out var value))
            return value;

        if (_defaultEntry is not null)
            return _defaultEntry.Value;

        return base.Get(key);
    }

    /// <inheritdoc/>
    public override T Get(int id)
    {
        if (TryGet(id, out var value))
            return value;

        if (_defaultEntry is not null)
            return _defaultEntry.Value;

        return base.Get(id);
    }

    /// <inheritdoc/>
    public override void Freeze()
    {
        if (DefaultKey is { } defaultKey)
            _defaultEntry = GetEntry(defaultKey);

        base.Freeze();
    }
}
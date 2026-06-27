namespace PanguEngine.Registries;

/// <summary>
/// Stores values by resource key and falls back to a default entry after freezing.
/// </summary>
/// <typeparam name="T">The registered value type.</typeparam>
public class DefaultedRegistry<T> : Registry<T> where T : class
{
    private RegistryEntry<T>? _defaultEntry;

    /// <summary>
    /// Creates a defaulted registry with the specified key and default entry key.
    /// </summary>
    /// <param name="key">The key that identifies this registry.</param>
    /// <param name="defaultKey">The key of the default entry.</param>
    public DefaultedRegistry(ResourceKey key, ResourceKey defaultKey) : base(key)
    {
        if (!ResourceKey.IsValid(defaultKey))
            throw new ArgumentException("Invalid registry key.", nameof(defaultKey));

        DefaultKey = defaultKey;
    }

    /// <summary>The key of the default entry.</summary>
    public ResourceKey DefaultKey { get; }

    /// <summary>The cached default entry.</summary>
    /// <exception cref="InvalidOperationException">The default entry is not available.</exception>
    public RegistryEntry<T> DefaultEntry =>
        _defaultEntry ?? throw new InvalidOperationException("Default entry is not available.");

    /// <summary>The cached default value.</summary>
    /// <exception cref="InvalidOperationException">The default entry is not available.</exception>
    public T DefaultValue => DefaultEntry.Value;

    /// <summary>The registry-local identifier of the default entry.</summary>
    /// <exception cref="InvalidOperationException">The default entry is not available.</exception>
    public int DefaultId => DefaultEntry.Id;

    /// <inheritdoc/>
    public override T Get(ResourceKey key)
    {
        if (TryGet(key, out var value))
            return value;

        if (_defaultEntry is not null)
            return _defaultEntry.Value;

        throw new KeyNotFoundException($"Resource key '{key}' is not registered.");
    }

    /// <inheritdoc/>
    public override T Get(int id)
    {
        if (TryGet(id, out var value))
            return value;

        if (_defaultEntry is not null)
            return _defaultEntry.Value;

        throw new KeyNotFoundException($"Registry id '{id}' is not registered.");
    }

    /// <inheritdoc/>
    public override RegistryEntry<T> GetEntry(ResourceKey key)
    {
        if (TryGetEntry(key, out var entry))
            return entry;

        if (_defaultEntry is not null)
            return _defaultEntry;

        throw new KeyNotFoundException($"Resource key '{key}' is not registered.");
    }

    /// <inheritdoc/>
    public override RegistryEntry<T> GetEntry(int id)
    {
        if (TryGetEntry(id, out var entry))
            return entry;

        if (_defaultEntry is not null)
            return _defaultEntry;

        throw new KeyNotFoundException($"Registry id '{id}' is not registered.");
    }

    /// <inheritdoc/>
    public override void Freeze()
    {
        _defaultEntry = base.GetEntry(DefaultKey);

        base.Freeze();
    }
}
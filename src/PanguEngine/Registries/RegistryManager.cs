using System.Diagnostics.CodeAnalysis;

namespace PanguEngine.Registries;

/// <summary>
/// Owns the engine registries and resolves registry-backed holders.
/// </summary>
public sealed class RegistryManager
{
    private readonly Registry<IWritableRegistry> _registries;
    private readonly List<IHolder> _pendingHolders = [];

    /// <summary>
    /// Creates a registry manager with the built-in registry catalog.
    /// </summary>
    public RegistryManager()
    {
        _registries = new Registry<IWritableRegistry>(RegistryKeys.Registries);
        _registries.Register(_registries.Key, _registries);
    }

    /// <summary>The registry catalog that stores all registered registries.</summary>
    public IRegistry<IWritableRegistry> Registries => _registries;

    /// <summary>Whether all registries have been frozen.</summary>
    public bool IsFrozen => _registries.IsFrozen;

    /// <summary>
    /// Registers a writable registry in the registry catalog.
    /// </summary>
    /// <param name="registry">The registry to register.</param>
    public void Register(IWritableRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registries.Register(registry.Key, registry);
    }

    /// <summary>
    /// Creates a holder for a registry entry address.
    /// </summary>
    /// <typeparam name="T">The referenced value type.</typeparam>
    /// <param name="address">The registry entry address.</param>
    /// <returns>The created holder.</returns>
    public Holder<T> CreateHolder<T>(ResourceAddress address) where T : class
    {
        ArgumentNullException.ThrowIfNull(address);

        var holder = Holder<T>.Reference(address);
        var pending = (IHolder)holder;

        if (IsFrozen)
        {
            pending.Resolve(this);
            return holder;
        }

        _pendingHolders.Add(pending);
        return holder;
    }

    /// <summary>
    /// Creates a holder for a registry entry key.
    /// </summary>
    /// <typeparam name="T">The referenced value type.</typeparam>
    /// <param name="registryKey">The key of the registry that contains the entry.</param>
    /// <param name="entryKey">The key of the referenced entry.</param>
    /// <returns>The created holder.</returns>
    public Holder<T> CreateHolder<T>(ResourceKey registryKey, ResourceKey entryKey) where T : class
    {
        return CreateHolder<T>(new ResourceAddress(registryKey, entryKey));
    }

    /// <summary>
    /// Gets a registry by key.
    /// </summary>
    /// <param name="key">The key of the registry.</param>
    /// <returns>The registry with the specified key.</returns>
    public IRegistry Get(ResourceKey key)
    {
        return TryGet(key, out var registry)
            ? registry
            : throw new KeyNotFoundException($"Registry key '{key}' is not registered.");
    }

    /// <summary>
    /// Attempts to get a registry by key.
    /// </summary>
    /// <param name="key">The key of the registry.</param>
    /// <param name="registry">The registry when found.</param>
    /// <returns>Whether the registry was found.</returns>
    public bool TryGet(ResourceKey key, [NotNullWhen(true)] out IRegistry? registry)
    {
        if (_registries.TryGet(key, out var writable))
        {
            registry = writable;
            return true;
        }

        registry = null;
        return false;
    }

    /// <summary>
    /// Gets a typed registry by key.
    /// </summary>
    /// <typeparam name="T">The value type stored by the registry.</typeparam>
    /// <param name="key">The key of the registry.</param>
    /// <returns>The typed registry with the specified key.</returns>
    public IRegistry<T> Get<T>(ResourceKey key) where T : class
    {
        var registry = Get(key);
        return registry as IRegistry<T> ?? throw new InvalidOperationException(
            $"Registry key '{key}' stores '{registry.ValueType}' values, not '{typeof(T)}'.");
    }

    /// <summary>
    /// Attempts to get a typed registry by key.
    /// </summary>
    /// <typeparam name="T">The value type stored by the registry.</typeparam>
    /// <param name="key">The key of the registry.</param>
    /// <param name="registry">The typed registry when found.</param>
    /// <returns>Whether the typed registry was found.</returns>
    public bool TryGet<T>(ResourceKey key, [NotNullWhen(true)] out IRegistry<T>? registry) where T : class
    {
        if (TryGet(key, out var found) && found is IRegistry<T> typed)
        {
            registry = typed;
            return true;
        }

        registry = null;
        return false;
    }

    /// <summary>
    /// Gets a typed writable registry by key.
    /// </summary>
    /// <typeparam name="T">The value type stored by the registry.</typeparam>
    /// <param name="key">The key of the registry.</param>
    /// <returns>The typed writable registry with the specified key.</returns>
    public IWritableRegistry<T> GetWritable<T>(ResourceKey key) where T : class
    {
        var registry = _registries.Get(key);
        return registry as IWritableRegistry<T> ?? throw new InvalidOperationException(
            $"Registry key '{key}' stores '{registry.ValueType}' values, not '{typeof(T)}'.");
    }

    /// <summary>
    /// Attempts to get a typed writable registry by key.
    /// </summary>
    /// <typeparam name="T">The value type stored by the registry.</typeparam>
    /// <param name="key">The key of the registry.</param>
    /// <param name="registry">The typed writable registry when found.</param>
    /// <returns>Whether the typed writable registry was found.</returns>
    public bool TryGetWritable<T>(ResourceKey key, [NotNullWhen(true)] out IWritableRegistry<T>? registry)
        where T : class
    {
        if (_registries.TryGet(key, out var found) && found is IWritableRegistry<T> typed)
        {
            registry = typed;
            return true;
        }

        registry = null;
        return false;
    }

    /// <summary>
    /// Freezes every registered registry and resolves pending holders.
    /// </summary>
    public void FreezeAll()
    {
        if (!_registries.IsFrozen)
            _registries.Freeze();

        foreach (var entry in _registries.Entries)
        {
            var registry = entry.Value;
            if (ReferenceEquals(registry, _registries) || registry.IsFrozen)
                continue;

            registry.Freeze();
        }

        ResolvePendingHolders();
    }

    private void ResolvePendingHolders()
    {
        foreach (var holder in _pendingHolders)
            holder.Resolve(this);

        _pendingHolders.Clear();
    }
}
using System.Diagnostics.CodeAnalysis;

namespace PanguEngine.Registries;

public sealed class RegistryManager
{
    private readonly Registry<IWritableRegistry> _registries;

    public RegistryManager()
    {
        _registries = new Registry<IWritableRegistry>(RegistryKeys.Registries);
        _registries.Register(_registries.Key, _registries);
    }

    public IRegistry<IWritableRegistry> Registries => _registries;

    public bool IsFrozen => _registries.IsFrozen;

    public void Register(IWritableRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registries.Register(registry.Key, registry);
    }

    public IRegistry Get(ResourceKey key)
    {
        return TryGet(key, out var registry)
            ? registry
            : throw new KeyNotFoundException($"Registry key '{key}' is not registered.");
    }

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

    public IRegistry<T> Get<T>(ResourceKey key) where T : class
    {
        var registry = Get(key);
        return registry is IRegistry<T> typed
            ? typed
            : throw new InvalidOperationException(
                $"Registry key '{key}' stores '{registry.ValueType}' values, not '{typeof(T)}'.");
    }

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

    public IWritableRegistry<T> GetWritable<T>(ResourceKey key) where T : class
    {
        var registry = _registries.Get(key);
        return registry is IWritableRegistry<T> typed
            ? typed
            : throw new InvalidOperationException(
                $"Registry key '{key}' stores '{registry.ValueType}' values, not '{typeof(T)}'.");
    }

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
    }
}
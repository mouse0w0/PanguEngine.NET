using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace PanguEngine.Registry;

/// <summary>
/// Stores values by resource key and registry-local numeric identifier.
/// </summary>
/// <typeparam name="T">The registered value type.</typeparam>
public class Registry<T> : IRegistry<T>
{
    private readonly Dictionary<ResourceKey, RegistryEntry<T>> _keyToEntry = [];
    private readonly List<RegistryEntry<T>> _entries = [];
    private readonly ReadOnlyCollection<RegistryEntry<T>> _readOnlyEntries;

    /// <summary>
    /// Creates a registry with the specified key.
    /// </summary>
    /// <param name="key">The key that identifies this registry.</param>
    public Registry(ResourceKey key)
    {
        if (!ResourceKey.IsValid(key))
            throw new ArgumentException("Invalid registry key.", nameof(key));

        Key = key;
        _readOnlyEntries = _entries.AsReadOnly();
    }

    /// <inheritdoc/>
    public ResourceKey Key { get; }

    /// <inheritdoc/>
    public Type ValueType => typeof(T);

    /// <inheritdoc/>
    public int Count => _entries.Count;

    /// <inheritdoc/>
    public bool IsFrozen { get; private set; }

    /// <inheritdoc/>
    public IReadOnlyList<RegistryEntry<T>> Entries => _readOnlyEntries;

    /// <inheritdoc/>
    public RegistryEntry<T> Register(ResourceKey key, T value)
    {
        if (IsFrozen)
            throw new InvalidOperationException("Registry is frozen.");
        if (!ResourceKey.IsValid(key))
            throw new ArgumentException("Invalid registry key.", nameof(key));
        if (value is null)
            throw new ArgumentNullException(nameof(value));
        if (_keyToEntry.ContainsKey(key))
            throw new InvalidOperationException($"Resource key '{key}' is already registered.");

        var entry = new RegistryEntry<T>(_entries.Count, key, value);
        _entries.Add(entry);
        _keyToEntry.Add(key, entry);
        return entry;
    }

    /// <inheritdoc/>
    public virtual T Get(ResourceKey key) => GetEntry(key).Value;

    /// <inheritdoc/>
    public virtual T Get(int id) => GetEntry(id).Value;

    /// <inheritdoc/>
    public bool TryGet(ResourceKey key, [MaybeNullWhen(false)] out T value)
    {
        if (TryGetEntry(key, out var entry))
        {
            value = entry.Value;
            return true;
        }

        value = default;
        return false;
    }

    /// <inheritdoc/>
    public bool TryGet(int id, [MaybeNullWhen(false)] out T value)
    {
        if (TryGetEntry(id, out var entry))
        {
            value = entry.Value;
            return true;
        }

        value = default;
        return false;
    }

    /// <inheritdoc/>
    public RegistryEntry<T> GetEntry(ResourceKey key)
    {
        if (TryGetEntry(key, out var entry))
            return entry;

        throw new KeyNotFoundException($"Resource key '{key}' is not registered.");
    }

    /// <inheritdoc/>
    public RegistryEntry<T> GetEntry(int id)
    {
        if (TryGetEntry(id, out var entry))
            return entry;

        throw new KeyNotFoundException($"Registry id '{id}' is not registered.");
    }

    /// <inheritdoc/>
    public bool TryGetEntry(ResourceKey key, [NotNullWhen(true)] out RegistryEntry<T>? entry)
    {
        if (!ResourceKey.IsValid(key))
        {
            entry = null;
            return false;
        }

        return _keyToEntry.TryGetValue(key, out entry);
    }

    /// <inheritdoc/>
    public bool TryGetEntry(int id, [NotNullWhen(true)] out RegistryEntry<T>? entry)
    {
        if ((uint)id >= (uint)_entries.Count)
        {
            entry = null;
            return false;
        }

        entry = _entries[id];
        return true;
    }

    /// <inheritdoc/>
    public bool ContainsKey(ResourceKey key) => ResourceKey.IsValid(key) && _keyToEntry.ContainsKey(key);

    /// <inheritdoc/>
    public bool ContainsId(int id) => (uint)id < (uint)_entries.Count;

    /// <inheritdoc/>
    public virtual void Freeze()
    {
        IsFrozen = true;
    }
}
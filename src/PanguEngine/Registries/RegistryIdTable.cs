using System.Diagnostics.CodeAnalysis;

namespace PanguEngine.Registries;

/// <summary>
/// Associates current session identifiers with resource keys and local definitions.
/// </summary>
/// <typeparam name="T">The local definition type.</typeparam>
public sealed class RegistryIdTable<T> : IRegistryIdTable where T : class
{
    private readonly IRegistry<T> _registry;
    private RegistryIdEntry<T>[] _entries = [];
    private readonly Dictionary<T, int> _valueToId = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<ResourceKey, int> _keyToId = [];

    /// <summary>
    /// Creates an empty identifier table associated with a local registry.
    /// </summary>
    /// <param name="registry">The source registry.</param>
    public RegistryIdTable(IRegistry<T> registry)
    {
        _registry = registry;
    }

    /// <inheritdoc/>
    public ResourceKey Key => _registry.Key;

    /// <inheritdoc/>
    public Type ValueType => typeof(T);

    /// <inheritdoc/>
    public int Count => _entries.Length;

    /// <summary>The current bound entries, ordered by identifier after successful allocation or binding.</summary>
    public IReadOnlyList<RegistryIdEntry<T>> Entries => _entries;

    /// <summary>
    /// Allocates consecutive identifiers in the frozen registry's registration order.
    /// </summary>
    public void Allocate()
    {
        EnsureFrozen();
        Clear();
        _entries = new RegistryIdEntry<T>[_registry.Count];
        for (var id = 0; id < _entries.Length; id++)
        {
            var entry = _registry.Entries[id];
            AddEntry(new RegistryIdEntry<T>(id, entry.Key, entry.Value));
        }
    }

    /// <summary>
    /// Binds an existing identifier mapping to definitions in a frozen local registry.
    /// </summary>
    /// <param name="mapping">The authoritative mapping with resource keys ordered by identifier.</param>
    public void Bind(RegistryIdMapping mapping)
    {
        EnsureFrozen();
        if (mapping.RegistryKey != Key)
            throw new InvalidOperationException($"Mapping for '{mapping.RegistryKey}' does not describe '{Key}'.");

        Clear();
        _entries = new RegistryIdEntry<T>[mapping.Keys.Count];
        for (var id = 0; id < _entries.Length; id++)
        {
            var key = mapping.Keys[id];
            if (!_registry.TryGetEntry(key, out var entry))
                throw new KeyNotFoundException($"Resource key '{key}' is not registered in '{Key}'.");

            AddEntry(new RegistryIdEntry<T>(id, key, entry.Value));
        }
    }

    /// <inheritdoc/>
    public void Clear()
    {
        _entries = [];
        _valueToId.Clear();
        _keyToId.Clear();
    }

    /// <summary>
    /// Gets a local definition by its session identifier.
    /// </summary>
    /// <param name="id">The session identifier.</param>
    /// <returns>The bound definition.</returns>
    public T Get(int id) => GetEntry(id).Value;

    /// <summary>
    /// Attempts to get a local definition by its session identifier.
    /// </summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="value">The bound definition when found.</param>
    /// <returns>Whether the identifier is bound.</returns>
    public bool TryGet(int id, [MaybeNullWhen(false)] out T value)
    {
        if (TryGetEntry(id, out var entry))
        {
            value = entry.Value;
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Gets the identifier of a bound definition instance.
    /// </summary>
    /// <param name="value">The bound definition instance.</param>
    /// <returns>The session identifier.</returns>
    public int GetId(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return _valueToId.TryGetValue(value, out var id)
            ? id
            : throw new KeyNotFoundException($"Value instance is not bound in '{Key}'.");
    }

    /// <summary>
    /// Attempts to get the identifier of a bound definition instance.
    /// </summary>
    /// <param name="value">The definition instance to find.</param>
    /// <param name="id">The session identifier, or -1 when not found.</param>
    /// <returns>Whether the instance is bound.</returns>
    public bool TryGetId(T? value, out int id)
    {
        if (value is not null && _valueToId.TryGetValue(value, out id))
            return true;

        id = -1;
        return false;
    }

    /// <summary>
    /// Gets the identifier associated with a resource key using value equality.
    /// </summary>
    /// <param name="key">The resource key of a bound definition.</param>
    /// <returns>The session identifier.</returns>
    public int GetId(ResourceKey key) =>
        _keyToId.TryGetValue(key, out var id)
            ? id
            : throw new KeyNotFoundException($"Resource key '{key}' is not bound in '{Key}'.");

    /// <summary>
    /// Attempts to get the identifier associated with a resource key using value equality.
    /// </summary>
    /// <param name="key">The resource key to find, or null.</param>
    /// <param name="id">The session identifier, or -1 when not found.</param>
    /// <returns>Whether the key is bound.</returns>
    public bool TryGetId(ResourceKey? key, out int id)
    {
        if (key is not null && _keyToId.TryGetValue(key, out id))
            return true;

        id = -1;
        return false;
    }

    /// <summary>
    /// Gets the resource key associated with an identifier.
    /// </summary>
    /// <param name="id">The session identifier.</param>
    /// <returns>The bound resource key.</returns>
    public ResourceKey GetKey(int id) => GetEntry(id).Key;

    /// <summary>
    /// Gets the resource key of a bound definition instance.
    /// </summary>
    /// <param name="value">The bound definition instance.</param>
    /// <returns>The bound resource key.</returns>
    public ResourceKey GetKey(T value) => _entries[GetId(value)].Key;

    /// <summary>
    /// Gets the entry associated with an identifier.
    /// </summary>
    /// <param name="id">The session identifier.</param>
    /// <returns>The bound entry.</returns>
    public RegistryIdEntry<T> GetEntry(int id) =>
        TryGetEntry(id, out var entry)
            ? entry
            : throw new KeyNotFoundException($"Registry id '{id}' is not bound in '{Key}'.");

    /// <summary>
    /// Attempts to get the entry associated with an identifier.
    /// </summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="entry">The bound entry, or the default value when not found.</param>
    /// <returns>Whether the identifier is bound.</returns>
    public bool TryGetEntry(int id, out RegistryIdEntry<T> entry)
    {
        if ((uint)id < (uint)_entries.Length)
        {
            entry = _entries[id];
            return true;
        }

        entry = default;
        return false;
    }

    /// <inheritdoc/>
    public RegistryIdMapping ExportMapping() =>
        new(Key, _entries.Select(entry => entry.Key).ToArray());

    private void AddEntry(RegistryIdEntry<T> entry)
    {
        if (!_keyToId.TryAdd(entry.Key, entry.Id))
            throw new InvalidOperationException($"Resource key '{entry.Key}' occurs more than once.");

        _entries[entry.Id] = entry;
        _valueToId.Add(entry.Value, entry.Id);
    }

    private void EnsureFrozen()
    {
        if (!_registry.IsFrozen)
            throw new InvalidOperationException($"Registry '{Key}' must be frozen before assigning ids.");
    }
}

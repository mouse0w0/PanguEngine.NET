using System.Diagnostics.CodeAnalysis;

namespace PanguEngine.Registries;

/// <summary>
/// Provides non-generic access to a registry.
/// </summary>
public interface IRegistry
{
    /// <summary>The key that identifies this registry.</summary>
    ResourceKey Key { get; }

    /// <summary>The value type stored by this registry.</summary>
    Type ValueType { get; }

    /// <summary>The number of registered entries.</summary>
    int Count { get; }

    /// <summary>Whether the registry no longer accepts new entries.</summary>
    bool IsFrozen { get; }
}

/// <summary>
/// Provides non-generic write access to a registry.
/// </summary>
public interface IWritableRegistry : IRegistry
{
    /// <summary>Prevents further entries from being registered.</summary>
    void Freeze();
}

/// <summary>
/// Provides typed access to a registry.
/// </summary>
/// <typeparam name="T">The registered value type.</typeparam>
public interface IRegistry<T> : IRegistry where T : class
{
    /// <summary>The registered entries ordered by their registry-local identifiers.</summary>
    IReadOnlyList<RegistryEntry<T>> Entries { get; }

    /// <summary>
    /// Gets a registered value by resource key.
    /// </summary>
    /// <param name="key">The key of the value.</param>
    /// <returns>The registered value.</returns>
    /// <remarks><see cref="DefaultedRegistry{T}"/> falls back to its default value after freezing.</remarks>
    T Get(ResourceKey key);

    /// <summary>
    /// Gets a registered value by registry-local identifier.
    /// </summary>
    /// <param name="id">The registry-local identifier.</param>
    /// <returns>The registered value.</returns>
    /// <remarks><see cref="DefaultedRegistry{T}"/> falls back to its default value after freezing.</remarks>
    T Get(int id);

    /// <summary>
    /// Attempts to get a registered value by resource key.
    /// </summary>
    /// <param name="key">The key of the value.</param>
    /// <param name="value">The registered value when found.</param>
    /// <returns>Whether the value was found.</returns>
    /// <remarks>This method does not use <see cref="DefaultedRegistry{T}"/> default fallback.</remarks>
    bool TryGet(ResourceKey key, [MaybeNullWhen(false)] out T value);

    /// <summary>
    /// Attempts to get a registered value by registry-local identifier.
    /// </summary>
    /// <param name="id">The registry-local identifier.</param>
    /// <param name="value">The registered value when found.</param>
    /// <returns>Whether the value was found.</returns>
    /// <remarks>This method does not use <see cref="DefaultedRegistry{T}"/> default fallback.</remarks>
    bool TryGet(int id, [MaybeNullWhen(false)] out T value);

    /// <summary>
    /// Gets a registry entry by resource key.
    /// </summary>
    /// <param name="key">The key of the entry.</param>
    /// <returns>The registry entry.</returns>
    /// <remarks><see cref="DefaultedRegistry{T}"/> falls back to its default entry after freezing.</remarks>
    RegistryEntry<T> GetEntry(ResourceKey key);

    /// <summary>
    /// Gets a registry entry by registry-local identifier.
    /// </summary>
    /// <param name="id">The registry-local identifier.</param>
    /// <returns>The registry entry.</returns>
    /// <remarks><see cref="DefaultedRegistry{T}"/> falls back to its default entry after freezing.</remarks>
    RegistryEntry<T> GetEntry(int id);

    /// <summary>
    /// Attempts to get a registry entry by resource key.
    /// </summary>
    /// <param name="key">The key of the entry.</param>
    /// <param name="entry">The registry entry when found.</param>
    /// <returns>Whether the entry was found.</returns>
    /// <remarks>This method does not use <see cref="DefaultedRegistry{T}"/> default fallback.</remarks>
    bool TryGetEntry(ResourceKey key, [NotNullWhen(true)] out RegistryEntry<T>? entry);

    /// <summary>
    /// Attempts to get a registry entry by registry-local identifier.
    /// </summary>
    /// <param name="id">The registry-local identifier.</param>
    /// <param name="entry">The registry entry when found.</param>
    /// <returns>Whether the entry was found.</returns>
    /// <remarks>This method does not use <see cref="DefaultedRegistry{T}"/> default fallback.</remarks>
    bool TryGetEntry(int id, [NotNullWhen(true)] out RegistryEntry<T>? entry);

    /// <summary>
    /// Gets the key of a registered value instance.
    /// </summary>
    /// <param name="value">The registered value instance.</param>
    /// <returns>The key of the registered value.</returns>
    /// <remarks>This reverse lookup does not use <see cref="DefaultedRegistry{T}"/> default fallback.</remarks>
    ResourceKey GetKey(T value);

    /// <summary>
    /// Gets the registry-local identifier of a registered value instance.
    /// </summary>
    /// <param name="value">The registered value instance.</param>
    /// <returns>The registry-local identifier of the registered value.</returns>
    /// <remarks>This reverse lookup does not use <see cref="DefaultedRegistry{T}"/> default fallback.</remarks>
    int GetId(T value);

    /// <summary>
    /// Attempts to get the key of a registered value instance.
    /// </summary>
    /// <param name="value">The registered value instance.</param>
    /// <param name="key">The key when the value instance was found.</param>
    /// <returns>Whether the value instance was found.</returns>
    /// <remarks>This method does not use <see cref="DefaultedRegistry{T}"/> default fallback.</remarks>
    bool TryGetKey(T value, [NotNullWhen(true)] out ResourceKey? key);

    /// <summary>
    /// Attempts to get the registry-local identifier of a registered value instance.
    /// </summary>
    /// <param name="value">The registered value instance.</param>
    /// <param name="id">The registry-local identifier when the value instance was found.</param>
    /// <returns>Whether the value instance was found.</returns>
    /// <remarks>This method does not use <see cref="DefaultedRegistry{T}"/> default fallback.</remarks>
    bool TryGetId(T value, out int id);

    /// <summary>
    /// Gets whether a resource key has been registered.
    /// </summary>
    /// <param name="key">The key to inspect.</param>
    /// <returns>Whether the key has been registered.</returns>
    bool ContainsKey(ResourceKey key);

    /// <summary>
    /// Gets whether a registry-local identifier has been registered.
    /// </summary>
    /// <param name="id">The registry-local identifier to inspect.</param>
    /// <returns>Whether the identifier has been registered.</returns>
    bool ContainsId(int id);
}

/// <summary>
/// Provides typed write access to a registry.
/// </summary>
/// <typeparam name="T">The registered value type.</typeparam>
public interface IWritableRegistry<T> : IRegistry<T>, IWritableRegistry where T : class
{
    /// <summary>
    /// Registers a value with a resource key.
    /// </summary>
    /// <param name="key">The key that identifies the value.</param>
    /// <param name="value">The value to register.</param>
    /// <returns>The created registry entry.</returns>
    RegistryEntry<T> Register(ResourceKey key, T value);
}
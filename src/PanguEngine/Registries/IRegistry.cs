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
    /// <summary>The registered entries in registration order.</summary>
    IReadOnlyList<RegistryEntry<T>> Entries { get; }

    /// <summary>
    /// Gets a registered value by resource key.
    /// </summary>
    /// <param name="key">The key of the value.</param>
    /// <returns>The registered value.</returns>
    /// <remarks><see cref="DefaultedRegistry{T}"/> falls back to its default value after freezing.</remarks>
    T Get(ResourceKey key);

    /// <summary>
    /// Attempts to get a registered value by resource key.
    /// </summary>
    /// <param name="key">The key of the value.</param>
    /// <param name="value">The registered value when found.</param>
    /// <returns>Whether the value was found.</returns>
    /// <remarks>This method does not use <see cref="DefaultedRegistry{T}"/> default fallback.</remarks>
    bool TryGet(ResourceKey key, [MaybeNullWhen(false)] out T value);

    /// <summary>
    /// Gets a registry entry by resource key.
    /// </summary>
    /// <param name="key">The key of the entry.</param>
    /// <returns>The registry entry.</returns>
    /// <remarks><see cref="DefaultedRegistry{T}"/> falls back to its default entry after freezing.</remarks>
    RegistryEntry<T> GetEntry(ResourceKey key);

    /// <summary>
    /// Attempts to get a registry entry by resource key.
    /// </summary>
    /// <param name="key">The key of the entry.</param>
    /// <param name="entry">The registry entry when found.</param>
    /// <returns>Whether the entry was found.</returns>
    /// <remarks>This method does not use <see cref="DefaultedRegistry{T}"/> default fallback.</remarks>
    bool TryGetEntry(ResourceKey key, [NotNullWhen(true)] out RegistryEntry<T>? entry);

    /// <summary>
    /// Gets the key of a registered value instance.
    /// </summary>
    /// <param name="value">The registered value instance.</param>
    /// <returns>The key of the registered value.</returns>
    /// <remarks>This reverse lookup does not use <see cref="DefaultedRegistry{T}"/> default fallback.</remarks>
    ResourceKey GetKey(T value);

    /// <summary>
    /// Attempts to get the key of a registered value instance.
    /// </summary>
    /// <param name="value">The registered value instance.</param>
    /// <param name="key">The key when the value instance was found.</param>
    /// <returns>Whether the value instance was found.</returns>
    /// <remarks>This method does not use <see cref="DefaultedRegistry{T}"/> default fallback.</remarks>
    bool TryGetKey(T value, [NotNullWhen(true)] out ResourceKey? key);

    /// <summary>
    /// Gets whether a resource key has been registered.
    /// </summary>
    /// <param name="key">The key to inspect.</param>
    /// <returns>Whether the key has been registered.</returns>
    bool ContainsKey(ResourceKey key);
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

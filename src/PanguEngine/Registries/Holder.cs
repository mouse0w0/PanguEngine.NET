using System.Diagnostics.CodeAnalysis;

namespace PanguEngine.Registries;

/// <summary>
/// Holds either a direct value or a registry entry value resolved by the registry manager.
/// </summary>
/// <typeparam name="T">The held value type.</typeparam>
public abstract class Holder<T> where T : class
{
    private protected Holder()
    {
    }

    /// <summary>The registry address for referenced values, or null for direct values.</summary>
    public abstract ResourceAddress? Address { get; }

    /// <summary>Whether this holder currently exposes a value.</summary>
    public abstract bool IsBound { get; }

    /// <summary>The held value.</summary>
    public abstract T Value { get; }

    /// <summary>
    /// Attempts to get the held value.
    /// </summary>
    /// <param name="value">The held value when this holder is bound.</param>
    /// <returns>Whether this holder is bound.</returns>
    public abstract bool TryGet([MaybeNullWhen(false)] out T value);

    /// <summary>
    /// Creates a holder for a direct value.
    /// </summary>
    /// <param name="value">The value to hold.</param>
    /// <returns>The created holder.</returns>
    public static Holder<T> Direct(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new DirectHolder(value);
    }

    internal static Holder<T> Reference(ResourceAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return new ReferenceHolder(address);
    }

    private sealed class DirectHolder(T value) : Holder<T>
    {
        public override ResourceAddress? Address => null;

        public override bool IsBound => true;

        public override T Value => value;

        public override bool TryGet([MaybeNullWhen(false)] out T result)
        {
            result = value;
            return true;
        }
    }

    private sealed class ReferenceHolder(ResourceAddress address) : Holder<T>, IHolder
    {
        private T? _value;

        public override ResourceAddress Address { get; } = address;

        public override bool IsBound => _value is not null;

        public override T Value => _value ?? throw new InvalidOperationException("Holder is not bound.");

        public override bool TryGet([MaybeNullWhen(false)] out T value)
        {
            if (_value is null)
            {
                value = null;
                return false;
            }

            value = _value;
            return true;
        }

        void IHolder.Resolve(RegistryManager manager)
        {
            if (_value is not null)
                throw new InvalidOperationException("Holder is already bound.");

            var registry = manager.Get<T>(Address.RegistryKey);
            if (!registry.TryGet(Address.EntryKey, out var value))
                throw new KeyNotFoundException($"Resource key '{Address.EntryKey}' is not registered.");

            _value = value;
        }
    }
}
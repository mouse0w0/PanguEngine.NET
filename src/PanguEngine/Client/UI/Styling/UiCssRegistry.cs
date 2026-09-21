using System.Runtime.CompilerServices;
using PanguEngine.Client.UI.Controls;

namespace PanguEngine.Client.UI.Styling;

/// <summary>
/// Registers CSS element names and property definitions for UI node types.
/// </summary>
/// <remarks>
/// Definitions are registered from control static constructors, resolved along the CLR base type chain and
/// frozen on first lookup so that published binding caches cannot be invalidated by later registrations.
/// </remarks>
public static class UiCssRegistry
{
    private static readonly Lock RegistryLock = new();
    private static readonly Dictionary<Type, Dictionary<string, PropertyDefinition>> PropertyRegistry = [];
    private static readonly Dictionary<Type, string> ElementNames = [];
    private static readonly HashSet<Type> FrozenTypes = [];

    /// <summary>
    /// Registers a stable CSS element name for a node type.
    /// </summary>
    /// <typeparam name="TNode">The node type to name.</typeparam>
    /// <param name="name">The ASCII identifier element name, compared with ordinal case sensitivity.</param>
    /// <exception cref="ArgumentException">Thrown when the name is not a valid ASCII identifier.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the target type is frozen or already has a registered element name.
    /// </exception>
    public static void RegisterElement<TNode>(string name)
        where TNode : UiNode
    {
        UiStyleIdentifier.ThrowIfInvalid(name, nameof(name));
        var targetType = typeof(TNode);
        lock (RegistryLock)
        {
            if (FrozenTypes.Contains(targetType))
                throw new InvalidOperationException(
                    $"CSS definitions for '{targetType}' are frozen and cannot register element name '{name}'.");

            if (!ElementNames.TryAdd(targetType, name))
                throw new InvalidOperationException(
                    $"An element name is already registered for '{targetType}'.");
        }
    }

    /// <summary>
    /// Registers a CSS property that converts text into a strongly typed value.
    /// </summary>
    /// <typeparam name="TTarget">The node type on which the CSS property applies.</typeparam>
    /// <typeparam name="TValue">The property value type.</typeparam>
    /// <param name="name">The CSS property name.</param>
    /// <param name="property">The property assigned by the converted value.</param>
    /// <param name="converter">The converter from a trimmed CSS value to the typed value.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="property"/> or <paramref name="converter"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the name is invalid, the property is read-only, invalidates input, or targets a type that
    /// <typeparamref name="TTarget"/> cannot be stored on.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the target type is frozen or the target/name pair is already registered.
    /// </exception>
    public static void RegisterProperty<TTarget, TValue>(
        string name,
        UiProperty<TValue> property,
        Func<string, TValue> converter)
        where TTarget : UiNode
    {
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(converter);
        UiStyleIdentifier.ThrowIfInvalid(name, nameof(name));
        if (property.IsReadOnly)
            throw new ArgumentException(
                $"Property '{property.Name}' is read-only and cannot be exposed to CSS.", nameof(property));
        if (property.Invalidation.HasFlag(UiPropertyInvalidation.Input))
            throw new ArgumentException(
                $"Property '{property.Name}' invalidates input and cannot be exposed to CSS.", nameof(property));
        if (!property.TargetType.IsAssignableFrom(typeof(TTarget)))
            throw new ArgumentException(
                $"Property '{property.Name}' targets '{property.TargetType}' which is not assignable from '{typeof(TTarget)}'.",
                nameof(property));

        Publish(
            name,
            typeof(TTarget),
            value => new[] { UiStyleSetter.Create(property, converter(value)) });
    }

    /// <summary>
    /// Registers a CSS property whose converter expands into one or more style setters.
    /// </summary>
    /// <typeparam name="TTarget">The node type on which the CSS property applies.</typeparam>
    /// <param name="name">The CSS property name.</param>
    /// <param name="converter">The converter from a trimmed CSS value to the resulting setters.</param>
    /// <remarks>
    /// The converter is not executed during registration; its output is validated when a declaration binds.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="converter"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the name is invalid.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the target type is frozen or the target/name pair is already registered.
    /// </exception>
    public static void RegisterProperty<TTarget>(
        string name,
        Func<string, IReadOnlyList<UiStyleSetter>> converter)
        where TTarget : UiNode
    {
        ArgumentNullException.ThrowIfNull(converter);
        UiStyleIdentifier.ThrowIfInvalid(name, nameof(name));
        Publish(name, typeof(TTarget), converter);
    }

    /// <summary>
    /// Initializes the CSS definitions for a node type and prevents subsequent registration on its type chain.
    /// </summary>
    /// <param name="targetType">The node type whose definitions are fixed.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="targetType"/> is null.</exception>
    internal static void EnsureInitialized(Type targetType)
    {
        ArgumentNullException.ThrowIfNull(targetType);

        lock (RegistryLock)
        {
            if (FrozenTypes.Contains(targetType))
                return;
        }

        for (var current = targetType;
             current is not null && typeof(UiNode).IsAssignableFrom(current);
             current = current.BaseType)
        {
            RuntimeHelpers.RunClassConstructor(current.TypeHandle);
        }

        RuntimeHelpers.RunClassConstructor(typeof(Canvas).TypeHandle);

        lock (RegistryLock)
        {
            for (var current = targetType;
                 current is not null && typeof(UiNode).IsAssignableFrom(current);
                 current = current.BaseType)
            {
                FrozenTypes.Add(current);
            }
        }
    }

    /// <summary>
    /// Finds the nearest CLR type in the base chain whose element name matches ordinally.
    /// </summary>
    /// <param name="runtimeType">The node runtime type the search starts from.</param>
    /// <param name="name">The case-sensitive element name to match.</param>
    /// <returns>The nearest matching type, or null when no level matches.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="runtimeType"/> or <paramref name="name"/> is null.</exception>
    internal static Type? MatchElement(Type runtimeType, string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        EnsureInitialized(runtimeType);

        lock (RegistryLock)
        {
            for (var current = runtimeType;
                 current is not null && typeof(UiNode).IsAssignableFrom(current);
                 current = current.BaseType)
            {
                var currentName = ElementNames.TryGetValue(current, out var registered)
                    ? registered
                    : current.Name;
                if (string.Equals(currentName, name, StringComparison.Ordinal))
                    return current;
            }

            return null;
        }
    }

    /// <summary>
    /// Finds the CSS property nearest to a target type for a case-insensitive CSS name.
    /// </summary>
    /// <param name="targetType">The node type being styled.</param>
    /// <param name="name">The CSS property name to resolve.</param>
    /// <returns>The closest registered definition, or null when none is registered for the name.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="targetType"/> or <paramref name="name"/> is null.</exception>
    internal static PropertyDefinition? FindProperty(Type targetType, string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        EnsureInitialized(targetType);

        lock (RegistryLock)
        {
            for (var current = targetType;
                 current is not null && typeof(UiNode).IsAssignableFrom(current);
                 current = current.BaseType)
            {
                if (PropertyRegistry.TryGetValue(current, out var properties) &&
                    properties.TryGetValue(name, out var property))
                {
                    return property;
                }
            }

            return null;
        }
    }

    private static void Publish(
        string name,
        Type targetType,
        Func<string, IReadOnlyList<UiStyleSetter>> converter)
    {
        var definition = new PropertyDefinition(name, targetType, converter);
        lock (RegistryLock)
        {
            if (FrozenTypes.Contains(targetType))
                throw new InvalidOperationException(
                    $"CSS definitions for '{targetType}' are frozen and cannot register '{name}'.");

            if (PropertyRegistry.TryGetValue(targetType, out var existing) && existing.ContainsKey(name))
                throw new InvalidOperationException(
                    $"A CSS property named '{name}' is already registered for '{targetType}'.");

            if (existing is null)
            {
                existing = new Dictionary<string, PropertyDefinition>(StringComparer.OrdinalIgnoreCase);
                PropertyRegistry[targetType] = existing;
            }

            existing[name] = definition;
        }
    }

    /// <summary>
    /// Holds one registered CSS property name, target type and converting delegate.
    /// </summary>
    internal sealed class PropertyDefinition
    {
        private readonly Func<string, IReadOnlyList<UiStyleSetter>> _converter;

        internal PropertyDefinition(
            string name,
            Type targetType,
            Func<string, IReadOnlyList<UiStyleSetter>> converter)
        {
            Name = name;
            TargetType = targetType;
            _converter = converter;
        }

        internal string Name { get; }

        internal Type TargetType { get; }

        internal IReadOnlyList<UiStyleSetter> Convert(string value) =>
            Array.AsReadOnly(_converter(value).ToArray());
    }
}

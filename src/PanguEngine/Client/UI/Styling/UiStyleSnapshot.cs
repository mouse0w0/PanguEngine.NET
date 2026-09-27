using PanguEngine.ComponentModel;

namespace PanguEngine.Client.UI.Styling;

/// <summary>
/// Provides an immutable per-node view of the winning declarations from the active style sheets.
/// </summary>
/// <remarks>
/// The snapshot exposes resolved values and their diagnostic sources but never a mutable declaration dictionary.
/// </remarks>
internal sealed class UiStyleSnapshot
{
    private readonly Dictionary<Property, object?> _values;
    private readonly Dictionary<Property, IReadOnlyList<UiStyleValueSource>> _sources;

    internal UiStyleSnapshot(Dictionary<Property, object?> values, Dictionary<Property, IReadOnlyList<UiStyleValueSource>> sources)
    {
        _values = values;
        _sources = sources;
    }

    internal bool TryGetBoxedValue(Property property, out object? value) =>
        _values.TryGetValue(property, out value);

    internal IEnumerable<Property> StyledProperties => _values.Keys;

    /// <summary>Gets the resolved style value for a property, or its descriptor default when no rule applies.</summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="property">The property descriptor.</param>
    /// <returns>The winning style value, or the descriptor default when no rule targets the property.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="property"/> is null.</exception>
    internal T GetValue<T>(Property<T> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        if (_values.TryGetValue(property, out var value))
            return value is null ? default! : (T)value;
        return property.DefaultValue;
    }

    /// <summary>Gets the winning declaration sources for the styled components of a property.</summary>
    /// <param name="property">The property descriptor.</param>
    /// <returns>The immutable sources in component order, or an empty list when no rule applies.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="property"/> is null.</exception>
    internal IReadOnlyList<UiStyleValueSource> GetSources(Property property)
    {
        ArgumentNullException.ThrowIfNull(property);
        return _sources.TryGetValue(property, out var sources) ? sources : Array.Empty<UiStyleValueSource>();
    }
}

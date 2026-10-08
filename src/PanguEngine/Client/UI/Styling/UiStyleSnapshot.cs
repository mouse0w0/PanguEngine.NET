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

    /// <summary>Validates the resolved property values and reports CSS rejection with declaration sources.</summary>
    /// <exception cref="ArgumentException">Thrown when a value from C# declarations is rejected.</exception>
    /// <exception cref="UiStyleParseException">Thrown when a value with CSS declarations is rejected.</exception>
    internal void ValidateValues()
    {
        foreach (var (property, value) in _values)
        {
            var error = property.GetBoxedValidationError(value);
            if (error is null)
                continue;

            var sources = GetSources(property);
            var cssSource = sources.FirstOrDefault(static source => source.CssPropertyName is not null);
            if (cssSource is not { SourceLocation: { } location })
                throw error;

            var declarations = string.Join(Environment.NewLine, sources.Select(source =>
                source.CssPropertyName is { } cssName && source.SourceLocation is { } sourceLocation
                    ? $"  {cssName} at {sourceLocation.SourceName ?? source.SheetSourceName}({sourceLocation.Line}:{sourceLocation.Column})"
                    : $"  {property.Name} ({source.Component}) from C# selector '{source.SelectorText}'").Distinct());
            throw new UiStyleParseException(
                UiStyleParseError.InvalidValue,
                location.SourceName ?? cssSource.SheetSourceName,
                location.Line,
                location.Column,
                location.Length,
                error,
                $"Reason: {error.Message}{Environment.NewLine}" +
                $"Winning declarations:{Environment.NewLine}{declarations}",
                propertyName: property.Name);
        }
    }

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

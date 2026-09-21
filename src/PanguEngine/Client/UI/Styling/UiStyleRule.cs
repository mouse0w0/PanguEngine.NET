namespace PanguEngine.Client.UI.Styling;

/// <summary>
/// Provides an immutable style rule with strongly typed setters or CSS declarations awaiting a matching node type.
/// </summary>
public sealed class UiStyleRule
{
    private readonly IReadOnlyList<CssDeclaration>? _cssDeclarations;

    /// <summary>Initializes a style rule.</summary>
    /// <param name="selector">The selector that matches target nodes.</param>
    /// <param name="setters">The setters; duplicate property and component pairs keep the last declaration.</param>
    /// <param name="sourceLocation">The optional source location for parsed rules.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="selector"/> or <paramref name="setters"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when a setter targets an incompatible or input-invalidating property, or a pseudo-state rule sets a layout property.
    /// </exception>
    public UiStyleRule(UiStyleSelector selector, IEnumerable<UiStyleSetter> setters, UiStyleSourceLocation? sourceLocation = null)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(setters);

        Selector = selector;
        SourceLocation = sourceLocation;

        var ordered = new List<UiStyleSetter>();
        foreach (var setter in setters)
        {
            ValidateSetter(selector.TargetType, selector.States, setter);
            for (var i = 0; i < ordered.Count; i++)
            {
                if (ReferenceEquals(ordered[i].Property, setter.Property) && ordered[i].Component == setter.Component)
                {
                    ordered.RemoveAt(i);
                    break;
                }
            }

            ordered.Add(setter);
        }

        Setters = ordered.AsReadOnly();
    }

    private UiStyleRule(
        UiStyleSelector selector,
        UiStyleSourceLocation sourceLocation,
        IReadOnlyList<CssDeclaration> declarations)
    {
        Selector = selector;
        SourceLocation = sourceLocation;
        Setters = Array.Empty<UiStyleSetter>();
        _cssDeclarations = Array.AsReadOnly(declarations.ToArray());
    }

    /// <summary>Gets the selector that matches target nodes.</summary>
    public UiStyleSelector Selector { get; }

    /// <summary>Gets the ordered, de-duplicated C# setters; parsed CSS rules expose no setters before binding.</summary>
    public IReadOnlyList<UiStyleSetter> Setters { get; }

    /// <summary>Gets the optional source location of the parsed rule.</summary>
    public UiStyleSourceLocation? SourceLocation { get; }

    internal int DeclarationCount => _cssDeclarations?.Count ?? Setters.Count;

    internal static UiStyleRule FromCss(
        UiStyleSelector selector,
        IReadOnlyList<CssDeclaration> declarations,
        UiStyleSourceLocation sourceLocation) =>
        new(selector, sourceLocation, declarations);

    internal IReadOnlyList<BoundDeclaration> Bind(Type targetType)
    {
        if (_cssDeclarations is null)
        {
            if (Selector.TargetType is null)
            {
                foreach (var setter in Setters)
                    ValidateSetterTarget(targetType, setter);
            }
            return Array.AsReadOnly(Setters.Select((setter, index) =>
                new BoundDeclaration(setter, index, null, SourceLocation)).ToArray());
        }

        var declarations = new List<BoundDeclaration>(_cssDeclarations.Count);
        for (var index = 0; index < _cssDeclarations.Count; index++)
        {
            var declaration = _cssDeclarations[index];
            var definition = UiCssRegistry.FindProperty(targetType, declaration.PropertyName);
            if (definition is null)
                continue;

            IReadOnlyList<UiStyleSetter> setters;
            try
            {
                setters = definition.Convert(declaration.Value);
            }
            catch (Exception exception)
            {
                throw CreateError(UiStyleParseError.InvalidValue, declaration.ValueLocation, exception);
            }

            var components = new HashSet<(UiProperty, UiStyleEdge?)>();
            foreach (var setter in setters)
            {
                try
                {
                    ValidateSetter(targetType, Selector.States, setter);
                    foreach (var component in setter.Expand())
                    {
                        if (!components.Add((component.Property, component.Component)))
                            throw new ArgumentException("A CSS declaration cannot assign the same style component twice.");
                    }
                }
                catch (Exception exception)
                {
                    throw CreateError(UiStyleParseError.InvalidValue, declaration.PropertyLocation, exception);
                }

                declarations.Add(new BoundDeclaration(setter, index, declaration.PropertyName, declaration.PropertyLocation));
            }
        }

        return declarations.AsReadOnly();
    }

    private static UiStyleParseException CreateError(
        UiStyleParseError error,
        UiStyleSourceLocation location,
        Exception? innerException = null) =>
        new(error, location.SourceName, location.Line, location.Column, location.Length, innerException);

    private static void ValidateSetter(Type? targetType, UiPseudoStates states, UiStyleSetter setter)
    {
        if (setter.Property.IsReadOnly)
            throw new ArgumentException(
                $"Property '{setter.Property.Name}' is read-only and cannot be styled.", nameof(setter));
        if (targetType is not null)
            ValidateSetterTarget(targetType, setter);
        if (setter.Property.Invalidation.HasFlag(UiPropertyInvalidation.Input))
            throw new ArgumentException(
                $"Property '{setter.Property.Name}' invalidates input and cannot be styled.", nameof(setter));
        if (states != UiPseudoStates.None &&
            (setter.Property.Invalidation.HasFlag(UiPropertyInvalidation.Measure) ||
             setter.Property.Invalidation.HasFlag(UiPropertyInvalidation.Arrange)))
        {
            throw new ArgumentException(
                $"Property '{setter.Property.Name}' invalidates layout and cannot be set by a pseudo-state rule.",
                nameof(setter));
        }
    }

    private static void ValidateSetterTarget(Type targetType, UiStyleSetter setter)
    {
        if (!setter.Property.TargetType.IsAssignableFrom(targetType))
            throw new ArgumentException(
                $"Property '{setter.Property.Name}' targets '{setter.Property.TargetType}' which is not assignable from selector target '{targetType}'.",
                nameof(setter));
    }

    internal readonly record struct CssDeclaration(
        string PropertyName,
        string Value,
        UiStyleSourceLocation PropertyLocation,
        UiStyleSourceLocation ValueLocation);

    internal readonly record struct BoundDeclaration(
        UiStyleSetter Setter,
        int DeclarationIndex,
        string? CssPropertyName,
        UiStyleSourceLocation? SourceLocation);
}

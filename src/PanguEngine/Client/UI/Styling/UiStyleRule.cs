using System.Collections.ObjectModel;

namespace PanguEngine.Client.UI.Styling;

/// <summary>
/// Provides an immutable style rule with one or more selectors sharing strongly typed setters or CSS declarations.
/// Cascade specificity is calculated from the matching selector branch without combining contributions across branches.
/// </summary>
public sealed class UiStyleRule
{
    private readonly ReadOnlyCollection<CssDeclaration>? _cssDeclarations;

    /// <summary>Initializes a style rule.</summary>
    /// <param name="selectors">The selectors that match target nodes.</param>
    /// <param name="setters">The setters; duplicate property and component pairs keep the last declaration.</param>
    /// <param name="sourceLocation">The optional source location for parsed rules.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="selectors"/> or <paramref name="setters"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the selector list is empty or contains null, when a setter targets an incompatible or input-invalidating
    /// property.
    /// </exception>
    public UiStyleRule(IEnumerable<UiStyleSelector> selectors, IEnumerable<UiStyleSetter> setters,
        UiStyleSourceLocation? sourceLocation = null)
    {
        ArgumentNullException.ThrowIfNull(selectors);
        ArgumentNullException.ThrowIfNull(setters);

        Selectors = CopySelectors(selectors);
        SourceLocation = sourceLocation;
        var ordered = new List<UiStyleSetter>();
        foreach (var setter in setters)
        {
            foreach (var selector in Selectors)
                ValidateSetter(selector.TargetType, setter);
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

    /// <summary>Initializes a style rule with one selector.</summary>
    /// <param name="selector">The selector that matches target nodes.</param>
    /// <param name="setters">The setters; duplicate property and component pairs keep the last declaration.</param>
    /// <param name="sourceLocation">The optional source location for parsed rules.</param>
    public UiStyleRule(UiStyleSelector selector, IEnumerable<UiStyleSetter> setters,
        UiStyleSourceLocation? sourceLocation = null)
        : this([selector], setters, sourceLocation)
    {
    }

    private UiStyleRule(
        IReadOnlyList<UiStyleSelector> selectors,
        UiStyleSourceLocation sourceLocation,
        IReadOnlyList<CssDeclaration> declarations)
    {
        Selectors = CopySelectors(selectors);
        SourceLocation = sourceLocation;
        Setters = Array.Empty<UiStyleSetter>();
        _cssDeclarations = Array.AsReadOnly(declarations.ToArray());
        HasVariables = _cssDeclarations.Any(declaration =>
            declaration.IsCustomProperty || declaration.Expression.HasVariables);
    }

    /// <summary>Gets the selectors that share this rule's declarations, in source order.</summary>
    public IReadOnlyList<UiStyleSelector> Selectors { get; }

    /// <summary>Gets the ordered, de-duplicated C# setters; parsed CSS rules expose no setters before binding.</summary>
    public IReadOnlyList<UiStyleSetter> Setters { get; }

    /// <summary>Gets the optional source location of the parsed rule.</summary>
    public UiStyleSourceLocation? SourceLocation { get; }

    /// <summary>Gets whether any selector in this rule contains a relationship combinator.</summary>
    internal bool HasRelationships => Selectors.Any(static selector => selector.HasRelationships);

    /// <summary>Gets whether any selector in this rule contains a sibling combinator.</summary>
    internal bool HasSiblingRelationships => Selectors.Any(static selector => selector.HasSiblingRelationships);

    internal int DeclarationCount => _cssDeclarations?.Count ?? Setters.Count;

    internal IReadOnlyList<CssDeclaration> CssDeclarations =>
        (IReadOnlyList<CssDeclaration>?)_cssDeclarations ?? Array.Empty<CssDeclaration>();

    internal bool HasVariables { get; }

    internal static UiStyleRule FromCss(
        IReadOnlyList<UiStyleSelector> selectors,
        IReadOnlyList<CssDeclaration> declarations,
        UiStyleSourceLocation sourceLocation) =>
        new(selectors, sourceLocation, declarations);

    internal IReadOnlyList<BoundDeclaration> Bind(Type targetType)
    {
        if (_cssDeclarations is null)
        {
            if (Selectors.Any(static selector => selector.TargetType is null))
            {
                foreach (var setter in Setters)
                    ValidateSetterTarget(targetType, setter);
            }

            return Array.AsReadOnly(Setters.Select((setter, index) =>
                new BoundDeclaration(setter, index, null, SourceLocation, false)).ToArray());
        }

        var declarations = new List<BoundDeclaration>(_cssDeclarations.Count);
        for (var index = 0; index < _cssDeclarations.Count; index++)
        {
            var declaration = _cssDeclarations[index];
            if (declaration.IsCustomProperty || declaration.Expression.HasVariables)
                continue;
            var definition = UiCssRegistry.FindProperty(targetType, declaration.PropertyName);
            if (definition is null)
                continue;
            declaration = PrepareImportant(declaration);
            declarations.AddRange(ConvertDeclaration(targetType, declaration, index, definition,
                () => declaration.Expression.Text));
        }

        return declarations.AsReadOnly();
    }

    private static ReadOnlyCollection<UiStyleSelector> CopySelectors(IEnumerable<UiStyleSelector> selectors)
    {
        var copied = selectors.ToArray();
        if (copied.Length == 0)
            throw new ArgumentException("A style rule must contain at least one selector.", nameof(selectors));
        if (copied.Any(static selector => selector is null))
            throw new ArgumentException("A style rule cannot contain a null selector.", nameof(selectors));
        return Array.AsReadOnly(copied);
    }

    internal IReadOnlyList<BoundVariableDeclaration> BindVariables(Type targetType)
    {
        var result = new List<BoundVariableDeclaration>();
        for (var index = 0; index < CssDeclarations.Count; index++)
        {
            var declaration = CssDeclarations[index];
            if (declaration.IsCustomProperty || !declaration.Expression.HasVariables)
                continue;
            if (UiCssRegistry.FindProperty(targetType, declaration.PropertyName) is { } definition)
                result.Add(new BoundVariableDeclaration(PrepareImportant(declaration), index, definition, targetType));
        }

        return result.AsReadOnly();
    }

    internal IReadOnlyList<BoundDeclaration> BindVariable(
        BoundVariableDeclaration declaration, UiCssVariableEnvironment variables) =>
        ConvertDeclaration(declaration.TargetType, declaration.Declaration, declaration.DeclarationIndex,
            declaration.Definition, () => variables.Substitute(declaration.Declaration.Expression));

    private ReadOnlyCollection<BoundDeclaration> ConvertDeclaration(
        Type targetType, CssDeclaration declaration, int index, UiCssRegistry.PropertyDefinition definition,
        Func<string> getValue)
    {
        IReadOnlyList<UiStyleSetter> setters;
        try
        {
            setters = definition.Convert(getValue());
        }
        catch (Exception exception)
        {
            throw CreateError(UiStyleParseError.InvalidValue, declaration.ValueLocation, exception);
        }

        var result = new List<BoundDeclaration>(setters.Count);
        var components = new HashSet<(UiProperty, UiStyleEdge?)>();
        foreach (var setter in setters)
        {
            try
            {
                ValidateSetter(targetType, setter);
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

            result.Add(new BoundDeclaration(setter, index, declaration.PropertyName, declaration.PropertyLocation,
                declaration.IsImportant));
        }

        return result.AsReadOnly();
    }

    private static UiStyleParseException CreateError(
        UiStyleParseError error,
        UiStyleSourceLocation location,
        Exception? innerException = null) =>
        new(error, location.SourceName, location.Line, location.Column, location.Length, innerException);

    internal static CssDeclaration PrepareImportant(CssDeclaration declaration)
    {
        try
        {
            var (expression, important) = declaration.Expression.ExtractImportant(declaration.IsCustomProperty);
            return declaration with { Expression = expression, IsImportant = important };
        }
        catch (FormatException exception)
        {
            throw CreateError(UiStyleParseError.InvalidValue, declaration.ValueLocation, exception);
        }
    }

    private static void ValidateSetter(Type? targetType, UiStyleSetter setter)
    {
        if (setter.Property.IsReadOnly)
            throw new ArgumentException(
                $"Property '{setter.Property.Name}' is read-only and cannot be styled.", nameof(setter));
        if (targetType is not null)
            ValidateSetterTarget(targetType, setter);
        if (setter.Property.Invalidation.HasFlag(UiPropertyInvalidation.Input))
            throw new ArgumentException(
                $"Property '{setter.Property.Name}' invalidates input and cannot be styled.", nameof(setter));
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
        UiCssValue Expression,
        UiStyleSourceLocation PropertyLocation,
        UiStyleSourceLocation ValueLocation,
        bool IsImportant = false)
    {
        internal bool IsCustomProperty => PropertyName.StartsWith("--", StringComparison.Ordinal);
    }

    internal readonly record struct BoundVariableDeclaration(
        CssDeclaration Declaration,
        int DeclarationIndex,
        UiCssRegistry.PropertyDefinition Definition,
        Type TargetType);

    internal readonly record struct BoundDeclaration(
        UiStyleSetter Setter,
        int DeclarationIndex,
        string? CssPropertyName,
        UiStyleSourceLocation? SourceLocation,
        bool IsImportant);
}
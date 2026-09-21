using PanguEngine.Resources;

namespace PanguEngine.Client.UI.Styling;

/// <summary>
/// Resolves declarations from immutable base and author style sheet snapshots.
/// </summary>
internal sealed class UiStyleResolver
{
    private static readonly UiStyleSheet DefaultStyleSheet = CreateDefaultStyleSheet();
    internal static UiStyleResolver Default { get; } = new([DefaultStyleSheet], []);

    private readonly List<RuleEntry> _rules;
    private readonly Lock _bindingSync = new();
    private readonly Dictionary<Type, IReadOnlyList<DeclarationEntry>> _boundDeclarations = [];

    internal UiStyleResolver(
        IEnumerable<UiStyleSheet> baseStyleSheets,
        IEnumerable<UiStyleSheet> styleSheets)
    {
        BaseStyleSheets = Array.AsReadOnly(baseStyleSheets.ToArray());
        StyleSheets = Array.AsReadOnly(styleSheets.ToArray());
        _rules = [];
        AddRules(BaseStyleSheets, UiStyleOrigin.Base);
        AddRules(StyleSheets, UiStyleOrigin.Author);
    }

    internal IReadOnlyList<UiStyleSheet> BaseStyleSheets { get; }

    internal IReadOnlyList<UiStyleSheet> StyleSheets { get; }

    private void AddRules(IReadOnlyList<UiStyleSheet> sheets, UiStyleOrigin origin)
    {
        for (var sheetIndex = 0; sheetIndex < sheets.Count; sheetIndex++)
        {
            var declarationIndex = 0;
            var rules = sheets[sheetIndex].Rules;
            for (var ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
            {
                var rule = rules[ruleIndex];
                _rules.Add(new RuleEntry(origin, sheetIndex, ruleIndex, rule, declarationIndex));
                declarationIndex += rule.DeclarationCount;
            }
        }
    }

    /// <summary>Resolves the winning style declarations for the supplied node.</summary>
    /// <param name="node">The node whose runtime type, classes, id and pseudo states select rules.</param>
    /// <returns>An immutable snapshot of the resolved style values and their sources.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="node"/> is null.</exception>
    internal UiStyleSnapshot Resolve(UiNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var winners = new Dictionary<(UiProperty Property, UiStyleEdge? Component), (CascadeKey Key, DeclarationEntry Entry)>();
        foreach (var entry in GetBoundDeclarations(node))
        {
            if (!entry.Rule.Selector.MatchesConditions(node))
                continue;
            var key = new CascadeKey(
                entry.Origin,
                entry.Rule.Selector.IdCount,
                entry.Rule.Selector.ClassAndPseudoCount,
                entry.TargetTypeDepth,
                entry.SheetIndex,
                entry.DeclarationIndex);
            var component = (entry.Setter.Property, entry.Setter.Component);
            if (winners.TryGetValue(component, out var current))
            {
                if (current.Key.CompareTo(key) < 0)
                    winners[component] = (key, entry);
            }
            else
            {
                winners[component] = (key, entry);
            }
        }

        var values = new Dictionary<UiProperty, object?>(winners.Count);
        var sourceLists = new Dictionary<UiProperty, List<UiStyleValueSource>>();
        foreach (var ((property, component), (_, entry)) in winners)
        {
            values.TryGetValue(property, out var value);
            values[property] = entry.Setter.Apply(value ?? property.DefaultValue);
            if (!sourceLists.TryGetValue(property, out var propertySources))
            {
                propertySources = [];
                sourceLists.Add(property, propertySources);
            }

            propertySources.Add(new UiStyleValueSource(
                entry.Rule.Selector,
                (entry.Origin == UiStyleOrigin.Base ? BaseStyleSheets : StyleSheets)[entry.SheetIndex].SourceName,
                entry.Origin,
                entry.SheetIndex,
                entry.RuleIndex,
                entry.DeclarationIndex,
                entry.SourceLocation,
                component,
                entry.CssPropertyName,
                isMaskedByLocalValue: false));
        }

        var sources = new Dictionary<UiProperty, IReadOnlyList<UiStyleValueSource>>(sourceLists.Count);
        foreach (var (property, propertySources) in sourceLists)
        {
            propertySources.Sort((left, right) => Nullable.Compare(left.Component, right.Component));
            sources.Add(property, propertySources.AsReadOnly());
        }
        return new UiStyleSnapshot(values, sources);
    }

    private IReadOnlyList<DeclarationEntry> GetBoundDeclarations(UiNode node)
    {
        var nodeType = node.GetType();
        lock (_bindingSync)
        {
            if (_boundDeclarations.TryGetValue(nodeType, out var cached))
                return cached;
        }

        UiCssRegistry.EnsureInitialized(nodeType);

        lock (_bindingSync)
        {
            if (_boundDeclarations.TryGetValue(nodeType, out var cached))
                return cached;

            var declarations = new List<DeclarationEntry>();
            foreach (var entry in _rules)
            {
                var targetType = entry.Rule.Selector.MatchTargetType(nodeType);
                if (targetType is null)
                    continue;

                var bound = entry.Rule.Bind(targetType);
                var typeDepth = UiStyleSelector.ComputeTargetTypeDepth(targetType);
                foreach (var declaration in bound)
                {
                    foreach (var setter in declaration.Setter.Expand())
                    {
                        declarations.Add(new DeclarationEntry(
                            entry.Origin,
                            entry.SheetIndex,
                            entry.RuleIndex,
                            entry.Rule,
                            setter,
                            entry.DeclarationIndex + declaration.DeclarationIndex,
                            typeDepth,
                            declaration.CssPropertyName,
                            declaration.SourceLocation));
                    }
                }
            }

            if (_boundDeclarations.TryGetValue(nodeType, out var published))
                return published;
            var result = declarations.AsReadOnly();
            _boundDeclarations.Add(nodeType, result);
            return result;
        }
    }

    private static UiStyleSheet CreateDefaultStyleSheet()
    {
        using var source = new DirectoryResourceSource(AppContext.BaseDirectory);
        using var stream = source.Open("pangu/ui/default.css");
        return UiStyleSheet.Parse(stream, "pangu-default");
    }

    private readonly record struct RuleEntry(
        UiStyleOrigin Origin,
        int SheetIndex,
        int RuleIndex,
        UiStyleRule Rule,
        int DeclarationIndex);

    private readonly record struct DeclarationEntry(
        UiStyleOrigin Origin,
        int SheetIndex,
        int RuleIndex,
        UiStyleRule Rule,
        UiStyleSetter Setter,
        int DeclarationIndex,
        int TargetTypeDepth,
        string? CssPropertyName,
        UiStyleSourceLocation? SourceLocation);

    private readonly struct CascadeKey(
        UiStyleOrigin origin,
        int idCount,
        int classAndPseudoCount,
        int targetTypeDepth,
        int sheetIndex,
        int declarationOrder) : IComparable<CascadeKey>
    {
        private UiStyleOrigin Origin { get; } = origin;

        private int IdCount { get; } = idCount;

        private int ClassAndPseudoCount { get; } = classAndPseudoCount;

        private int TargetTypeDepth { get; } = targetTypeDepth;

        private int SheetIndex { get; } = sheetIndex;

        private int DeclarationOrder { get; } = declarationOrder;

        public int CompareTo(CascadeKey other)
        {
            var comparison = Origin.CompareTo(other.Origin);
            if (comparison != 0) return comparison;
            comparison = IdCount.CompareTo(other.IdCount);
            if (comparison != 0) return comparison;
            comparison = ClassAndPseudoCount.CompareTo(other.ClassAndPseudoCount);
            if (comparison != 0) return comparison;
            comparison = TargetTypeDepth.CompareTo(other.TargetTypeDepth);
            if (comparison != 0) return comparison;
            comparison = SheetIndex.CompareTo(other.SheetIndex);
            if (comparison != 0) return comparison;
            return DeclarationOrder.CompareTo(other.DeclarationOrder);
        }
    }
}

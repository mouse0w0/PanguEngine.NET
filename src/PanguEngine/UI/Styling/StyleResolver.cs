using PanguEngine.ComponentModel;
using PanguEngine.Resources;

namespace PanguEngine.UI.Styling;

/// <summary>
/// Resolves declarations from immutable base and author style sheet snapshots.
/// </summary>
internal sealed partial class StyleResolver
{
    private static readonly StyleSheet DefaultStyleSheet = CreateDefaultStyleSheet();
    internal static StyleResolver Default { get; } = new([DefaultStyleSheet], []);

    private readonly List<RuleEntry> _rules;
    private readonly Lock _bindingSync = new();
    private readonly Dictionary<Type, IReadOnlyList<BoundRuleEntry>> _boundRules = [];
    private long _cachedRegistryVersion = -1;

    internal StyleResolver(
        IEnumerable<StyleSheet> baseStyleSheets,
        IEnumerable<StyleSheet> styleSheets)
    {
        BaseStyleSheets = Array.AsReadOnly(baseStyleSheets.ToArray());
        StyleSheets = Array.AsReadOnly(styleSheets.ToArray());
        _rules = [];
        AddRules(BaseStyleSheets, StyleOrigin.Base);
        AddRules(StyleSheets, StyleOrigin.Author);
        InitializeVariableRules();
    }

    internal IReadOnlyList<StyleSheet> BaseStyleSheets { get; }

    internal IReadOnlyList<StyleSheet> StyleSheets { get; }

    /// <summary>Gets a value indicating whether any input rule contains a relationship combinator.</summary>
    internal bool HasRelationships { get; private set; }

    /// <summary>Gets a value indicating whether any input rule contains an adjacent or subsequent sibling combinator.</summary>
    internal bool HasSiblingRelationships { get; private set; }

    /// <summary>Gets whether any input rule requires the root pseudo class.</summary>
    internal bool HasRootPseudoClass { get; private set; }

    /// <summary>Gets whether any input rule depends on sibling positions or counts.</summary>
    internal bool HasChildIndexedPseudoClasses { get; private set; }

    /// <summary>Gets whether any input rule depends on the presence of child nodes.</summary>
    internal bool HasEmptyPseudoClass { get; private set; }

    private void AddRules(IReadOnlyList<StyleSheet> sheets, StyleOrigin origin)
    {
        for (var sheetIndex = 0; sheetIndex < sheets.Count; sheetIndex++)
        {
            var declarationIndex = 0;
            var rules = sheets[sheetIndex].Rules;
            for (var ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
            {
                var rule = rules[ruleIndex];
                HasRelationships |= rule.HasRelationships;
                HasSiblingRelationships |= rule.HasSiblingRelationships;
                HasRootPseudoClass |= rule.Selectors.Any(static selector => selector.HasRootPseudoClass);
                HasChildIndexedPseudoClasses |= rule.HasChildIndexedPseudoClasses;
                HasEmptyPseudoClass |= rule.HasEmptyPseudoClass;
                HasVariables |= rule.HasVariables;
                _rules.Add(new RuleEntry(origin, sheetIndex, ruleIndex, rule, declarationIndex));
                declarationIndex += rule.DeclarationCount;
            }
        }
    }

    /// <summary>Resolves the winning style declarations for the supplied node.</summary>
    /// <param name="node">The node whose runtime type, classes, id and pseudo states select rules.</param>
    /// <returns>An immutable snapshot of the resolved style values and their sources.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="node"/> is null.</exception>
    internal StyleSnapshot Resolve(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var variables = HasVariables
            ? ResolveVariables(node)
            : CssVariableEnvironment.Empty;
        var winners =
            new Dictionary<(Property Property, StyleEdge? Component), (CascadeKey Key, DeclarationEntry Entry)>();
        var variableBindings =
            new Dictionary<(StyleRule Rule, StyleRule.BoundVariableDeclaration Declaration),
                IReadOnlyList<StyleRule.BoundDeclaration>>();
        foreach (var rule in GetBoundRules(node))
        {
            if (!rule.Selector.TryMatch(node, out var typeDepth))
                continue;
            foreach (var entry in rule.Declarations)
                Consider(entry, typeDepth);
            foreach (var declaration in rule.VariableDeclarations)
            {
                var bindingKey = (rule.Entry.Rule, declaration);
                if (!variableBindings.TryGetValue(bindingKey, out var boundDeclarations))
                {
                    boundDeclarations = StyleRule.BindVariable(declaration, variables);
                    variableBindings.Add(bindingKey, boundDeclarations);
                }

                foreach (var bound in boundDeclarations)
                {
                    foreach (var setter in bound.Setter.Expand())
                    {
                        Consider(new DeclarationEntry(rule.Entry.Origin, rule.Entry.SheetIndex, rule.Entry.RuleIndex,
                            rule.Selector, setter, rule.Entry.DeclarationIndex + bound.DeclarationIndex,
                            bound.CssPropertyName, bound.SourceLocation, bound.IsImportant), typeDepth);
                    }
                }
            }
        }

        var values = new Dictionary<Property, object?>(winners.Count);
        var sourceLists = new Dictionary<Property, List<StyleValueSource>>();
        foreach (var ((property, component), (_, entry)) in winners)
        {
            values.TryGetValue(property, out var value);
            values[property] = entry.Setter.Apply(value ?? property.DefaultValue);
            if (!sourceLists.TryGetValue(property, out var propertySources))
            {
                propertySources = [];
                sourceLists.Add(property, propertySources);
            }

            propertySources.Add(new StyleValueSource(
                entry.Selector,
                (entry.Origin == StyleOrigin.Base ? BaseStyleSheets : StyleSheets)[entry.SheetIndex].SourceName,
                entry.Origin,
                entry.SheetIndex,
                entry.RuleIndex,
                entry.DeclarationIndex,
                entry.SourceLocation,
                component,
                entry.CssPropertyName,
                isMaskedByLocalValue: false));
        }

        var sources = new Dictionary<Property, IReadOnlyList<StyleValueSource>>(sourceLists.Count);
        foreach (var (property, propertySources) in sourceLists)
        {
            propertySources.Sort((left, right) => Nullable.Compare(left.Component, right.Component));
            sources.Add(property, propertySources.AsReadOnly());
        }

        return new StyleSnapshot(values, sources);

        void Consider(DeclarationEntry entry, int typeDepth)
        {
            var key = new CascadeKey(
                entry.IsImportant,
                entry.Origin,
                entry.Selector.IdCount,
                entry.Selector.ClassAndPseudoCount,
                typeDepth,
                entry.SheetIndex,
                entry.DeclarationIndex);
            var component = (entry.Setter.Property, entry.Setter.Component);
            if (!winners.TryGetValue(component, out var current) || current.Key.CompareTo(key) < 0)
                winners[component] = (key, entry);
        }
    }

    private IReadOnlyList<BoundRuleEntry> GetBoundRules(Node node)
    {
        var nodeType = node.GetType();

        lock (_bindingSync)
        {
            var version = CssRegistry.Version;
            if (_cachedRegistryVersion != version)
            {
                _boundRules.Clear();
                _cachedRegistryVersion = version;
            }

            if (_boundRules.TryGetValue(nodeType, out var cached))
                return cached;

            var rules = new List<BoundRuleEntry>();
            foreach (var entry in _rules)
            {
                var bindings = new Dictionary<Type, IReadOnlyList<StyleRule.BoundDeclaration>>();
                var variableBindings = new Dictionary<Type, IReadOnlyList<StyleRule.BoundVariableDeclaration>>();
                foreach (var selector in entry.Rule.Selectors)
                {
                    var targetType = selector.MatchTargetType(nodeType);
                    if (targetType is null)
                        continue;

                    if (!bindings.TryGetValue(targetType, out var bound))
                    {
                        bound = entry.Rule.Bind(targetType);
                        bindings.Add(targetType, bound);
                        variableBindings.Add(targetType, entry.Rule.BindVariables(targetType));
                    }

                    var declarations = new List<DeclarationEntry>();
                    foreach (var declaration in bound)
                    {
                        foreach (var setter in declaration.Setter.Expand())
                        {
                            declarations.Add(new DeclarationEntry(
                                entry.Origin,
                                entry.SheetIndex,
                                entry.RuleIndex,
                                selector,
                                setter,
                                entry.DeclarationIndex + declaration.DeclarationIndex,
                                declaration.CssPropertyName,
                                declaration.SourceLocation,
                                declaration.IsImportant));
                        }
                    }

                    var variableDeclarations = variableBindings[targetType];
                    if (declarations.Count > 0 || variableDeclarations.Count > 0)
                        rules.Add(new BoundRuleEntry(entry, selector, declarations.ToArray(), variableDeclarations));
                }
            }

            var result = rules.AsReadOnly();
            _boundRules[nodeType] = result;
            return result;
        }
    }

    private static StyleSheet CreateDefaultStyleSheet()
    {
        const string resourcePath = "pangu/ui/default.css";
        using var source = new DirectoryResourceSource(AppContext.BaseDirectory);
        using var stream = source.Open(resourcePath);
        return StyleSheet.Parse(stream, resourcePath);
    }

    private readonly record struct RuleEntry(
        StyleOrigin Origin,
        int SheetIndex,
        int RuleIndex,
        StyleRule Rule,
        int DeclarationIndex);

    private readonly record struct BoundRuleEntry(
        RuleEntry Entry,
        StyleSelector Selector,
        DeclarationEntry[] Declarations,
        IReadOnlyList<StyleRule.BoundVariableDeclaration> VariableDeclarations);

    private readonly record struct DeclarationEntry(
        StyleOrigin Origin,
        int SheetIndex,
        int RuleIndex,
        StyleSelector Selector,
        StyleSetter Setter,
        int DeclarationIndex,
        string? CssPropertyName,
        StyleSourceLocation? SourceLocation,
        bool IsImportant);

    private readonly struct CascadeKey(
        bool isImportant,
        StyleOrigin origin,
        int idCount,
        int classAndPseudoCount,
        int targetTypeDepth,
        int sheetIndex,
        int declarationOrder) : IComparable<CascadeKey>
    {
        private bool IsImportant { get; } = isImportant;

        private StyleOrigin Origin { get; } = origin;

        private int IdCount { get; } = idCount;

        private int ClassAndPseudoCount { get; } = classAndPseudoCount;

        private int TargetTypeDepth { get; } = targetTypeDepth;

        private int SheetIndex { get; } = sheetIndex;

        private int DeclarationOrder { get; } = declarationOrder;

        public int CompareTo(CascadeKey other)
        {
            var comparison = IsImportant.CompareTo(other.IsImportant);
            if (comparison != 0) return comparison;
            comparison = Origin.CompareTo(other.Origin);
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

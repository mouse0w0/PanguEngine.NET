namespace PanguEngine.Client.UI.Styling;

internal sealed partial class UiStyleResolver
{
    private readonly List<RuleEntry> _variableRules = [];

    internal bool HasVariables { get; private set; }

    private void InitializeVariableRules()
    {
        foreach (var entry in _rules)
        {
            if (entry.Rule.CssDeclarations.Any(declaration => declaration.IsCustomProperty))
                _variableRules.Add(entry);
        }
    }

    private UiCssVariableEnvironment ResolveVariables(UiNode node, UiCssVariableEnvironment parent)
    {
        var winners =
            new Dictionary<string, (CascadeKey Key, UiStyleRule.CssDeclaration Declaration)>(StringComparer.Ordinal);
        foreach (var entry in _variableRules)
        {
            foreach (var selector in entry.Rule.Selectors)
            {
                if (!selector.TryMatch(node, out var typeDepth))
                    continue;
                for (var index = 0; index < entry.Rule.CssDeclarations.Count; index++)
                {
                    var declaration = entry.Rule.CssDeclarations[index];
                    if (!declaration.IsCustomProperty)
                        continue;
                    declaration = UiStyleRule.PrepareImportant(declaration);
                    var key = new CascadeKey(declaration.IsImportant, entry.Origin, selector.IdCount,
                        selector.ClassAndPseudoCount, typeDepth, entry.SheetIndex,
                        entry.DeclarationIndex + index);
                    if (!winners.TryGetValue(declaration.PropertyName, out var current) ||
                        current.Key.CompareTo(key) < 0)
                        winners[declaration.PropertyName] = (key, declaration);
                }
            }
        }

        return UiCssVariableEnvironment.Create(parent,
            winners.ToDictionary(pair => pair.Key, pair => pair.Value.Declaration, StringComparer.Ordinal));
    }

    /// <summary>Provides consistent variable environments during a style preparation batch.</summary>
    internal sealed class VariableContext
    {
        private readonly Dictionary<(UiStyleResolver Resolver, UiNode Node), UiCssVariableEnvironment> _environments =
            [];

        internal UiCssVariableEnvironment Get(UiStyleResolver resolver, UiNode node)
        {
            if (_environments.TryGetValue((resolver, node), out var environment))
                return environment;
            var parent = node.Parent is { } ancestor ? Get(resolver, ancestor) : UiCssVariableEnvironment.Empty;
            environment = resolver.ResolveVariables(node, parent);
            _environments.Add((resolver, node), environment);
            return environment;
        }
    }
}
namespace PanguEngine.Client.UI.Styling;

/// <summary>Provides the computed, inherited custom properties of one node.</summary>
internal sealed class UiCssVariableEnvironment
{
    internal static UiCssVariableEnvironment Empty { get; } = new(new Dictionary<string, Value>(StringComparer.Ordinal));
    private readonly Dictionary<string, Value> _values;

    private UiCssVariableEnvironment(Dictionary<string, Value> values) => _values = values;

    internal string Substitute(UiCssValue expression) => expression.Substitute(
        name => _values.GetValueOrDefault(name)?.Text,
        name => _values.GetValueOrDefault(name)?.Error);

    internal static UiCssVariableEnvironment Create(
        UiCssVariableEnvironment parent,
        IReadOnlyDictionary<string, UiStyleRule.CssDeclaration> declarations)
    {
        if (declarations.Count == 0)
            return parent;

        var values = new Dictionary<string, Value>(parent._values, StringComparer.Ordinal);
        foreach (var name in declarations.Keys)
            values.Remove(name);

        var indices = new Dictionary<string, int>(StringComparer.Ordinal);
        var lowLinks = new Dictionary<string, int>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        var active = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in declarations.Keys)
        {
            if (!indices.ContainsKey(name))
                FindCycles(name);
        }
        foreach (var name in declarations.Keys)
            Resolve(name);
        return new UiCssVariableEnvironment(values);

        void FindCycles(string name)
        {
            indices.Add(name, indices.Count);
            lowLinks.Add(name, indices[name]);
            stack.Push(name);
            active.Add(name);
            foreach (var dependency in declarations[name].Expression.References)
            {
                if (!declarations.ContainsKey(dependency))
                    continue;
                if (!indices.TryGetValue(dependency, out var dependencyIndex))
                {
                    FindCycles(dependency);
                    lowLinks[name] = Math.Min(lowLinks[name], lowLinks[dependency]);
                }
                else if (active.Contains(dependency))
                    lowLinks[name] = Math.Min(lowLinks[name], dependencyIndex);
            }
            if (lowLinks[name] != indices[name])
                return;
            var component = new List<string>();
            string member;
            do
            {
                member = stack.Pop();
                active.Remove(member);
                component.Add(member);
            } while (member != name);
            if (component.Count == 1 && !declarations[name].Expression.References.Contains(name))
                return;
            var cycle = string.Join(", ", component);
            foreach (var cyclicName in component)
                values[cyclicName] = Invalid(cyclicName, $"Cyclic CSS variable dependency among: {cycle}.");
        }

        string? Resolve(string name)
        {
            if (values.TryGetValue(name, out var value))
                return value.Text;
            if (!declarations.TryGetValue(name, out var declaration))
                return null;
            try
            {
                var result = declaration.Expression.Substitute(Resolve, dependency => values.GetValueOrDefault(dependency)?.Error);
                values.Add(name, new Value(result, null));
                return result;
            }
            catch (FormatException exception)
            {
                values.Add(name, Invalid(name, exception.Message));
                return null;
            }
        }

        Value Invalid(string name, string reason)
        {
            var location = declarations[name].ValueLocation;
            return new Value(null,
                $"Variable '{name}' at {location.SourceName ?? "<stylesheet>"}:{location.Line}:{location.Column}: {reason}");
        }
    }

    private sealed record Value(string? Text, string? Error);
}

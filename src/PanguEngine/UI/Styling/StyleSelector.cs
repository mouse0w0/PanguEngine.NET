using System.Text;

namespace PanguEngine.UI.Styling;

/// <summary>Identifies the relationship between two compound selectors in a selector chain.</summary>
internal enum StyleCombinator
{
    /// <summary>Matches a strict ancestor, written as <c>A B</c>.</summary>
    Descendant,

    /// <summary>Matches the direct parent, written as <c>A &gt; B</c>.</summary>
    Child,

    /// <summary>Matches the immediately preceding sibling, written as <c>A + B</c>.</summary>
    AdjacentSibling,

    /// <summary>Matches any preceding sibling, written as <c>A ~ B</c>.</summary>
    SubsequentSibling
}

/// <summary>
/// Provides an immutable compound selector segment with a type constraint and additional element conditions.
/// </summary>
internal sealed class StyleSelectorSegment
{
    private readonly StyleSelectorCondition[] _conditions;

    private StyleSelectorSegment(
        Type? targetType,
        string typeName,
        StyleSelectorCondition[] conditions)
    {
        TargetType = targetType;
        TypeName = typeName;
        _conditions = conditions;
        foreach (var condition in conditions)
        {
            IdCount += condition.IdCount;
            ClassAndPseudoCount += condition.ClassAndPseudoCount;
            Dependencies |= condition.Dependencies;
        }

        Text = BuildText();
    }

    /// <summary>Gets the CLR target type of this segment, or null for a CSS segment.</summary>
    internal Type? TargetType { get; }

    /// <summary>Gets the CSS element name, or <c>*</c> when the element type is unrestricted.</summary>
    internal string TypeName { get; }

    /// <summary>Gets the combined structural dependencies of this segment's conditions.</summary>
    internal StyleSelectorDependencies Dependencies { get; }

    /// <summary>Gets the normalized text of this single segment.</summary>
    internal string Text { get; }

    /// <summary>Gets this segment's combined id contribution to specificity.</summary>
    internal int IdCount { get; }

    /// <summary>Gets this segment's combined class and pseudo-class contribution to specificity.</summary>
    internal int ClassAndPseudoCount { get; }

    /// <summary>Gets a value indicating whether this segment constrains the element type.</summary>
    internal bool HasTypeConstraint => TargetType is not null || TypeName != "*";

    /// <summary>Creates a normalized segment from raw conditions.</summary>
    internal static StyleSelectorSegment Create(
        Type? targetType,
        string typeName,
        IEnumerable<StyleSelectorCondition> conditions)
    {
        var seen = new HashSet<StyleSelectorCondition>();
        var distinct = new List<StyleSelectorCondition>();
        foreach (var condition in conditions)
        {
            if (!condition.IsEmpty && seen.Add(condition))
                distinct.Add(condition);
        }

        var normalized = distinct.OrderBy(static condition => condition, StyleSelectorCondition.NormalizationComparer).ToArray();
        return new StyleSelectorSegment(targetType, typeName, normalized);
    }

    /// <summary>Resolves the CLR type matched by this segment for the supplied element type, or null when unmatched.</summary>
    internal Type? MatchTargetType(Type nodeType)
    {
        if (TargetType != null)
            return TargetType.IsAssignableFrom(nodeType) ? TargetType : null;
        if (TypeName == "*")
            return nodeType;
        return CssRegistry.MatchElement(nodeType, TypeName);
    }

    /// <summary>Determines whether the supplied element satisfies every additional condition in this segment.</summary>
    internal bool MatchesConditions(UIElement node)
    {
        foreach (var condition in _conditions)
        {
            if (!condition.Matches(node))
                return false;
        }

        return true;
    }

    /// <summary>Matches this segment against a single element and reports its element contribution.</summary>
    internal bool TryMatch(UIElement node, out int elementDepth)
    {
        var matched = MatchTargetType(node.GetType());
        if (matched is null || !MatchesConditions(node))
        {
            elementDepth = 0;
            return false;
        }

        elementDepth = HasTypeConstraint ? StyleSelector.ComputeTargetTypeDepth(matched) : 0;
        return true;
    }

    private string BuildText()
    {
        var builder = new StringBuilder();
        if (HasTypeConstraint || _conditions.Length == 0)
            builder.Append(TypeName);
        foreach (var condition in _conditions)
            builder.Append(condition.Text);

        return builder.ToString();
    }
}

/// <summary>
/// Provides an immutable style selector chain with ordered compound selectors and combinators,
/// together with its cascade specificity.
/// </summary>
public sealed class StyleSelector
{
    private readonly IReadOnlyList<StyleSelectorSegment> _segments;
    private readonly IReadOnlyList<StyleCombinator> _combinators;

    private StyleSelector(
        IReadOnlyList<StyleSelectorSegment> segments,
        IReadOnlyList<StyleCombinator> combinators)
    {
        _segments = segments;
        _combinators = combinators;

        var idCount = 0;
        var classAndPseudoCount = 0;
        var dependencies = StyleSelectorDependencies.None;
        foreach (var segment in segments)
        {
            idCount += segment.IdCount;
            classAndPseudoCount += segment.ClassAndPseudoCount;
            dependencies |= segment.Dependencies;
        }

        IdCount = idCount;
        ClassAndPseudoCount = classAndPseudoCount;
        Dependencies = dependencies;
        HasRelationships = segments.Count > 1;

        var hasSiblingRelationships = false;
        foreach (var combinator in combinators)
        {
            if (combinator is StyleCombinator.AdjacentSibling or StyleCombinator.SubsequentSibling)
            {
                hasSiblingRelationships = true;
                break;
            }
        }

        HasSiblingRelationships = hasSiblingRelationships;
        SelectorText = BuildSelectorText();
    }

    /// <summary>Gets the CLR target type of the rightmost segment, or null for a CSS selector.</summary>
    internal Type? TargetType => _segments[^1].TargetType;

    /// <summary>Gets the combined id contribution to specificity summed across the whole chain.</summary>
    public int IdCount { get; }

    /// <summary>Gets the combined class and pseudo-class contribution to specificity summed across the whole chain.</summary>
    public int ClassAndPseudoCount { get; }

    /// <summary>Gets the normalized text of the whole chain, omitting an unrestricted wildcard when other conditions exist.</summary>
    public string SelectorText { get; }

    /// <summary>Gets a value indicating whether the chain contains any relationship combinator.</summary>
    internal bool HasRelationships { get; }

    /// <summary>Gets a value indicating whether the chain contains an adjacent or subsequent sibling combinator.</summary>
    internal bool HasSiblingRelationships { get; }

    /// <summary>Gets the combined structural dependencies of all conditions in the chain.</summary>
    internal StyleSelectorDependencies Dependencies { get; }

    /// <summary>Gets whether any segment in the chain requires the root pseudo class.</summary>
    internal bool HasRootPseudoClass => (Dependencies & StyleSelectorDependencies.Root) != 0;

    /// <summary>Gets whether any segment depends on sibling positions or counts.</summary>
    internal bool HasChildIndexedPseudoClasses => (Dependencies & StyleSelectorDependencies.ChildPosition) != 0;

    /// <summary>Gets whether any segment depends on the presence of child nodes.</summary>
    internal bool HasEmptyPseudoClass => (Dependencies & StyleSelectorDependencies.Children) != 0;

    /// <summary>Creates a selector for a target element type as a single-segment chain.</summary>
    /// <typeparam name="TNode">The target element type.</typeparam>
    /// <param name="conditions">The optional element conditions; the input is copied, deduplicated and ordered by normalization key.</param>
    /// <returns>A new immutable selector.</returns>
    public static StyleSelector For<TNode>(IEnumerable<StyleSelectorCondition>? conditions = null)
        where TNode : UIElement
    {
        var segment = StyleSelectorSegment.Create(typeof(TNode), typeof(TNode).Name, conditions ?? []);
        return Create([segment], []);
    }

    /// <summary>Creates a CSS selector chain from ordered segments and their separating combinators.</summary>
    internal static StyleSelector Create(
        IReadOnlyList<StyleSelectorSegment> segments,
        IReadOnlyList<StyleCombinator> combinators)
    {
        var normalizedSegments = Array.AsReadOnly(segments.ToArray());
        var normalizedCombinators = Array.AsReadOnly(combinators.ToArray());
        return new StyleSelector(normalizedSegments, normalizedCombinators);
    }

    /// <summary>Determines whether the supplied element matches this whole selector chain.</summary>
    /// <param name="node">The target element to test.</param>
    /// <returns>true when the element and its relationships satisfy the whole chain.</returns>
    public bool Matches(UIElement node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return TryMatch(node, out _);
    }

    /// <summary>Resolves the rightmost segment's matched CLR type for the supplied element type, or null when unmatched.</summary>
    internal Type? MatchTargetType(Type nodeType) => _segments[^1].MatchTargetType(nodeType);

    /// <summary>
    /// Matches the whole chain from the rightmost segment and reports the maximum element contribution
    /// across every successful matching path, or zero when the chain does not match.
    /// </summary>
    internal bool TryMatch(UIElement node, out int typeDepth) =>
        TryMatchAt(_segments.Count - 1, node, out typeDepth);

    private bool TryMatchAt(int index, UIElement node, out int typeDepth)
    {
        if (!_segments[index].TryMatch(node, out var ownDepth))
        {
            typeDepth = 0;
            return false;
        }

        if (index == 0)
        {
            typeDepth = ownDepth;
            return true;
        }

        var bestPrefixDepth = -1;
        switch (_combinators[index - 1])
        {
            case StyleCombinator.Child:
                if (node.Parent is { } parent)
                    TryMatchPrefix(index - 1, parent, ref bestPrefixDepth);
                break;
            case StyleCombinator.Descendant:
                for (var ancestor = node.Parent; ancestor is not null; ancestor = ancestor.Parent)
                    TryMatchPrefix(index - 1, ancestor, ref bestPrefixDepth);
                break;
            case StyleCombinator.AdjacentSibling:
                if (node.Parent is { } adjacentParent)
                {
                    var children = adjacentParent.ReadOnlyChildren;
                    var position = IndexOfChild(children, node);
                    if (position > 0)
                        TryMatchPrefix(index - 1, children[position - 1], ref bestPrefixDepth);
                }

                break;
            case StyleCombinator.SubsequentSibling:
                if (node.Parent is { } precedingParent)
                {
                    var children = precedingParent.ReadOnlyChildren;
                    var position = IndexOfChild(children, node);
                    for (var childIndex = 0; childIndex < position; childIndex++)
                        TryMatchPrefix(index - 1, children[childIndex], ref bestPrefixDepth);
                }

                break;
        }

        if (bestPrefixDepth < 0)
        {
            typeDepth = 0;
            return false;
        }

        typeDepth = ownDepth + bestPrefixDepth;
        return true;
    }

    private void TryMatchPrefix(int prefixIndex, UIElement candidate, ref int bestPrefixDepth)
    {
        if (TryMatchAt(prefixIndex, candidate, out var prefixDepth) && prefixDepth > bestPrefixDepth)
            bestPrefixDepth = prefixDepth;
    }

    private static int IndexOfChild(IReadOnlyList<UIElement> children, UIElement node)
    {
        for (var index = 0; index < children.Count; index++)
        {
            if (ReferenceEquals(children[index], node))
                return index;
        }

        return -1;
    }

    private string BuildSelectorText()
    {
        var builder = new StringBuilder(_segments[0].Text);
        for (var index = 1; index < _segments.Count; index++)
        {
            builder.Append(_combinators[index - 1] switch
            {
                StyleCombinator.Descendant => " ",
                StyleCombinator.Child => " > ",
                StyleCombinator.AdjacentSibling => " + ",
                _ => " ~ "
            });
            builder.Append(_segments[index].Text);
        }

        return builder.ToString();
    }

    internal static int ComputeTargetTypeDepth(Type type)
    {
        var depth = 1;
        var current = type;
        while (current != typeof(UIElement))
        {
            current = current.BaseType!;
            depth++;
        }

        return depth;
    }
}

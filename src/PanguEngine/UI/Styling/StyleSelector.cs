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
/// Provides an immutable compound selector segment that constrains a single node's element type, classes, id and pseudo classes.
/// </summary>
internal sealed class StyleSelectorSegment
{
    private StyleSelectorSegment(
        Type? targetType,
        string typeName,
        IReadOnlyList<string> classes,
        string? id,
        IReadOnlyList<PseudoClass> pseudoClasses,
        IReadOnlyList<StyleNthCondition> nthConditions)
    {
        TargetType = targetType;
        TypeName = typeName;
        Classes = classes;
        Id = id;
        PseudoClasses = pseudoClasses;
        NthConditions = nthConditions;
        Text = BuildText();
    }

    /// <summary>Gets the CLR target type of this segment, or null for a CSS segment.</summary>
    internal Type? TargetType { get; }

    /// <summary>Gets the CSS node name, or <c>*</c> when the element type is unrestricted.</summary>
    internal string TypeName { get; }

    /// <summary>Gets the required classes, deduplicated and kept in first-seen order.</summary>
    internal IReadOnlyList<string> Classes { get; }

    /// <summary>Gets the required id, or null when no id is required.</summary>
    internal string? Id { get; }

    /// <summary>Gets the required pseudo-class identifiers, deduplicated and sorted by ordinal name.</summary>
    internal IReadOnlyList<PseudoClass> PseudoClasses { get; }

    /// <summary>Gets the immutable sibling-position conditions in normalized order.</summary>
    internal IReadOnlyList<StyleNthCondition> NthConditions { get; }

    /// <summary>Gets the normalized text of this single segment.</summary>
    internal string Text { get; }

    /// <summary>Gets this segment's id contribution to specificity: 0 or 1.</summary>
    internal int IdCount => Id is null ? 0 : 1;

    /// <summary>Gets this segment's combined class and pseudo-class contribution to specificity.</summary>
    internal int ClassAndPseudoCount => Classes.Count + PseudoClasses.Count + NthConditions.Count;

    /// <summary>Gets a value indicating whether this segment requires any pseudo class.</summary>
    internal bool HasPseudoClasses => PseudoClasses.Count != 0 || NthConditions.Count != 0;

    /// <summary>Gets whether sibling positions or counts affect this segment's match.</summary>
    internal bool HasChildIndexedPseudoClasses =>
        NthConditions.Count != 0 || PseudoClasses.Any(static pseudo => pseudo.IsChildIndexed);

    /// <summary>Gets whether the presence of children affects this segment's match.</summary>
    internal bool HasEmptyPseudoClass => PseudoClasses.Contains(PseudoClass.Empty);

    /// <summary>Gets a value indicating whether this segment constrains the node element type.</summary>
    internal bool HasTypeConstraint => TargetType is not null || TypeName != "*";

    /// <summary>Creates a normalized segment from raw conditions.</summary>
    internal static StyleSelectorSegment Create(
        Type? targetType,
        string typeName,
        IEnumerable<string>? classes,
        string? id,
        IEnumerable<PseudoClass>? pseudoClasses,
        IEnumerable<StyleNthCondition>? nthConditions = null)
    {
        if (id is not null)
            StyleIdentifier.ThrowIfInvalid(id, nameof(id));

        var normalizedClasses = new List<string>();
        if (classes is not null)
        {
            foreach (var className in classes)
            {
                StyleIdentifier.ThrowIfInvalid(className, nameof(classes));
                if (!normalizedClasses.Contains(className))
                    normalizedClasses.Add(className);
            }
        }

        var orderedPseudoClasses = new List<PseudoClass>();
        if (pseudoClasses is not null)
        {
            foreach (var pseudoClass in pseudoClasses)
            {
                if (!orderedPseudoClasses.Contains(pseudoClass))
                    orderedPseudoClasses.Add(pseudoClass);
            }
        }

        orderedPseudoClasses.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.Name, right.Name));
        var orderedNthConditions = nthConditions?.Distinct().ToList() ?? [];
        orderedNthConditions.Sort(static (left, right) =>
        {
            var comparison = StringComparer.Ordinal.Compare(left.Name, right.Name);
            if (comparison != 0)
                return comparison;
            comparison = left.Expression.A.CompareTo(right.Expression.A);
            return comparison != 0 ? comparison : left.Expression.B.CompareTo(right.Expression.B);
        });
        return new StyleSelectorSegment(
            targetType,
            typeName,
            normalizedClasses.AsReadOnly(),
            id,
            orderedPseudoClasses.AsReadOnly(),
            orderedNthConditions.AsReadOnly());
    }

    /// <summary>Resolves the CLR type matched by this segment for the supplied node type, or null when unmatched.</summary>
    internal Type? MatchTargetType(Type nodeType)
    {
        if (TargetType != null)
            return TargetType.IsAssignableFrom(nodeType) ? TargetType : null;
        if (TypeName == "*")
            return nodeType;
        return CssRegistry.MatchElement(nodeType, TypeName);
    }

    /// <summary>Determines whether the supplied node satisfies this segment's id, class and pseudo-class conditions.</summary>
    internal bool MatchesConditions(Node node)
    {
        if (Id is not null && !string.Equals(Id, node.StyleId, StringComparison.Ordinal))
            return false;
        foreach (var className in Classes)
        {
            if (!node.Classes.Contains(className))
                return false;
        }

        foreach (var pseudoClass in PseudoClasses)
        {
            if (!node.HasPseudoClass(pseudoClass))
                return false;
        }

        foreach (var condition in NthConditions)
        {
            if (!condition.Matches(node))
                return false;
        }

        return true;
    }

    /// <summary>Matches this segment against a single node and reports its element contribution.</summary>
    internal bool TryMatch(Node node, out int elementDepth)
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
        if (HasTypeConstraint || (Classes.Count == 0 && Id is null && !HasPseudoClasses))
            builder.Append(TypeName);
        foreach (var className in Classes)
            builder.Append('.').Append(className);
        if (Id is not null)
            builder.Append('#').Append(Id);
        foreach (var pseudoClass in PseudoClasses)
            builder.Append(':').Append(pseudoClass.Name);
        foreach (var condition in NthConditions)
            builder.Append(':').Append(condition);

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
        IReadOnlyList<StyleCombinator> combinators,
        int targetTypeDepth)
    {
        _segments = segments;
        _combinators = combinators;
        TargetTypeDepth = targetTypeDepth;

        var target = segments[^1];
        TargetType = target.TargetType;
        TypeName = target.TypeName;
        Classes = target.Classes;
        Id = target.Id;
        PseudoClasses = target.PseudoClasses;

        var idCount = 0;
        var classAndPseudoCount = 0;
        var hasPseudoClasses = false;
        foreach (var segment in segments)
        {
            idCount += segment.IdCount;
            classAndPseudoCount += segment.ClassAndPseudoCount;
            hasPseudoClasses |= segment.HasPseudoClasses;
        }

        IdCount = idCount;
        ClassAndPseudoCount = classAndPseudoCount;
        HasPseudoClasses = hasPseudoClasses;
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
    public Type? TargetType { get; }

    /// <summary>
    /// Gets the CSS node name of the rightmost segment, <c>*</c> when the element type is unrestricted,
    /// or the simple name of the CLR target type.
    /// </summary>
    public string TypeName { get; }

    /// <summary>Gets the rightmost segment's required classes, deduplicated and kept in first-seen order.</summary>
    public IReadOnlyList<string> Classes { get; }

    /// <summary>Gets the rightmost segment's required id, or null when no id is required.</summary>
    public string? Id { get; }

    /// <summary>
    /// Gets the rightmost segment's required pseudo-class identifiers, deduplicated and sorted by ordinal name;
    /// every identifier must match the target node's state or structure. Function arguments are represented in <see cref="SelectorText"/>.
    /// </summary>
    public IReadOnlyList<PseudoClass> PseudoClasses { get; }

    /// <summary>Gets the id contribution to specificity summed across the whole chain, one per segment that requires an id.</summary>
    public int IdCount { get; }

    /// <summary>Gets the combined class and pseudo-class contribution to specificity summed across the whole chain.</summary>
    public int ClassAndPseudoCount { get; }

    /// <summary>
    /// Gets the inheritance distance from the rightmost CLR target type to <see cref="Node"/> plus one,
    /// or zero for a CSS selector. A CSS chain's element specificity is determined during matching.
    /// </summary>
    public int TargetTypeDepth { get; }

    /// <summary>Gets the normalized text of the whole chain, omitting an unrestricted wildcard when other conditions exist.</summary>
    public string SelectorText { get; }

    /// <summary>Gets a value indicating whether the chain contains any relationship combinator.</summary>
    internal bool HasRelationships { get; }

    /// <summary>Gets a value indicating whether the chain contains an adjacent or subsequent sibling combinator.</summary>
    internal bool HasSiblingRelationships { get; }

    /// <summary>Gets a value indicating whether any segment in the chain requires a pseudo class.</summary>
    internal bool HasPseudoClasses { get; }

    /// <summary>Gets whether any segment in the chain requires the root pseudo class.</summary>
    internal bool HasRootPseudoClass =>
        _segments.Any(static segment => segment.PseudoClasses.Contains(PseudoClass.Root));

    /// <summary>Gets whether any segment depends on sibling positions or counts.</summary>
    internal bool HasChildIndexedPseudoClasses =>
        _segments.Any(static segment => segment.HasChildIndexedPseudoClasses);

    /// <summary>Gets whether any segment depends on the presence of child nodes.</summary>
    internal bool HasEmptyPseudoClass => _segments.Any(static segment => segment.HasEmptyPseudoClass);

    /// <summary>Creates a selector for a target node type as a single-segment chain.</summary>
    /// <typeparam name="TNode">The target node type.</typeparam>
    /// <param name="classes">The optional required classes; duplicates are removed and invalid identifiers rejected.</param>
    /// <param name="id">The optional required id; must be an ASCII identifier.</param>
    /// <param name="pseudoClasses">
    /// The optional required pseudo-class identifiers; the input is copied, deduplicated and sorted by name.
    /// </param>
    /// <returns>A new immutable selector.</returns>
    /// <exception cref="ArgumentException">Thrown when a class or id is not a valid ASCII identifier.</exception>
    public static StyleSelector For<TNode>(
        IEnumerable<string>? classes = null,
        string? id = null,
        IEnumerable<PseudoClass>? pseudoClasses = null)
        where TNode : Node
    {
        var segment = StyleSelectorSegment.Create(typeof(TNode), typeof(TNode).Name, classes, id, pseudoClasses);
        return Create([segment], []);
    }

    /// <summary>Creates a CSS selector chain from ordered segments and their separating combinators.</summary>
    internal static StyleSelector Create(
        IReadOnlyList<StyleSelectorSegment> segments,
        IReadOnlyList<StyleCombinator> combinators)
    {
        var normalizedSegments = Array.AsReadOnly(segments.ToArray());
        var normalizedCombinators = Array.AsReadOnly(combinators.ToArray());
        var singleTargetType = normalizedSegments.Count == 1 ? normalizedSegments[0].TargetType : null;
        var targetTypeDepth = singleTargetType is not null ? ComputeTargetTypeDepth(singleTargetType) : 0;
        return new StyleSelector(normalizedSegments, normalizedCombinators, targetTypeDepth);
    }

    /// <summary>Determines whether the supplied node matches this whole selector chain.</summary>
    /// <param name="node">The target node to test.</param>
    /// <returns>true when the node and its relationships satisfy the whole chain.</returns>
    public bool Matches(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return TryMatch(node, out _);
    }

    /// <summary>Resolves the rightmost segment's matched CLR type for the supplied node type, or null when unmatched.</summary>
    internal Type? MatchTargetType(Type nodeType) => _segments[^1].MatchTargetType(nodeType);

    /// <summary>
    /// Matches the whole chain from the rightmost segment and reports the maximum element contribution
    /// across every successful matching path, or zero when the chain does not match.
    /// </summary>
    internal bool TryMatch(Node node, out int typeDepth) =>
        TryMatchAt(_segments.Count - 1, node, out typeDepth);

    private bool TryMatchAt(int index, Node node, out int typeDepth)
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

    private void TryMatchPrefix(int prefixIndex, Node candidate, ref int bestPrefixDepth)
    {
        if (TryMatchAt(prefixIndex, candidate, out var prefixDepth) && prefixDepth > bestPrefixDepth)
            bestPrefixDepth = prefixDepth;
    }

    private static int IndexOfChild(IReadOnlyList<Node> children, Node node)
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
        while (current != typeof(Node))
        {
            current = current.BaseType!;
            depth++;
        }

        return depth;
    }
}

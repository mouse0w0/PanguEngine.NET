using System.Text;

namespace PanguEngine.Client.UI.Styling;

/// <summary>
/// Provides an immutable single-node style selector and its cascade specificity.
/// </summary>
public sealed class UiStyleSelector
{
    private UiStyleSelector(
        Type? targetType,
        string typeName,
        IReadOnlyList<string> classes,
        string? id,
        UiPseudoStates states,
        int idCount,
        int classAndPseudoCount,
        int targetTypeDepth)
    {
        TargetType = targetType;
        TypeName = typeName;
        Classes = classes;
        Id = id;
        States = states;
        IdCount = idCount;
        ClassAndPseudoCount = classAndPseudoCount;
        TargetTypeDepth = targetTypeDepth;
    }

    /// <summary>Gets the CLR target type, or null for a CSS selector.</summary>
    public Type? TargetType { get; }

    /// <summary>
    /// Gets the CSS node name, <c>*</c> when the element type is unrestricted,
    /// or the simple name of the CLR target type.
    /// </summary>
    public string TypeName { get; }

    /// <summary>Gets the required classes, deduplicated and kept in first-seen order.</summary>
    public IReadOnlyList<string> Classes { get; }

    /// <summary>Gets the required id, or null when no id is required.</summary>
    public string? Id { get; }

    /// <summary>Gets the required pseudo states; every bit must be active to match.</summary>
    public UiPseudoStates States { get; }

    /// <summary>Gets the id contribution to specificity: 0 or 1.</summary>
    public int IdCount { get; }

    /// <summary>Gets the combined class and pseudo-state contribution to specificity.</summary>
    public int ClassAndPseudoCount { get; }

    /// <summary>
    /// Gets the inheritance distance from the CLR target type to <see cref="UiNode"/> plus one,
    /// or zero for a CSS selector. CSS element specificity is determined during binding.
    /// </summary>
    public int TargetTypeDepth { get; }

    /// <summary>Gets a value indicating whether the selector constrains the node element type.</summary>
    internal bool HasTypeConstraint => TargetType is not null || TypeName != "*";

    /// <summary>Creates a selector for a target node type.</summary>
    /// <typeparam name="TNode">The target node type.</typeparam>
    /// <param name="classes">The optional required classes; duplicates are removed and invalid identifiers rejected.</param>
    /// <param name="id">The optional required id; must be an ASCII identifier.</param>
    /// <param name="states">The optional required pseudo states.</param>
    /// <returns>A new immutable selector.</returns>
    /// <exception cref="ArgumentException">Thrown when a class or id is not a valid ASCII identifier.</exception>
    public static UiStyleSelector For<TNode>(
        IEnumerable<string>? classes = null,
        string? id = null,
        UiPseudoStates states = UiPseudoStates.None)
        where TNode : UiNode
        => Create(typeof(TNode), typeof(TNode).Name, classes, id, states);

    /// <summary>Creates a CSS selector with an element name or <c>*</c> for any element type.</summary>
    internal static UiStyleSelector Create(
        string typeName,
        IEnumerable<string>? classes,
        string? id,
        UiPseudoStates states) =>
        Create(null, typeName, classes, id, states);

    private static UiStyleSelector Create(
        Type? targetType,
        string typeName,
        IEnumerable<string>? classes,
        string? id,
        UiPseudoStates states)
    {
        if (id is not null)
            UiStyleIdentifier.ThrowIfInvalid(id, nameof(id));

        var normalizedClasses = new List<string>();
        if (classes is not null)
        {
            foreach (var className in classes)
            {
                UiStyleIdentifier.ThrowIfInvalid(className, nameof(classes));
                if (!normalizedClasses.Contains(className))
                    normalizedClasses.Add(className);
            }
        }

        return new UiStyleSelector(
            targetType,
            typeName,
            normalizedClasses.AsReadOnly(),
            id,
            states,
            id is null ? 0 : 1,
            normalizedClasses.Count + CountBits(states),
            targetType is null ? 0 : ComputeTargetTypeDepth(targetType));
    }

    /// <summary>Determines whether the supplied node matches this selector.</summary>
    /// <param name="node">The node to test.</param>
    /// <returns>true when the node type, classes, id and pseudo states all satisfy the selector.</returns>
    public bool Matches(UiNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return MatchTargetType(node.GetType()) is not null && MatchesConditions(node);
    }

    internal Type? MatchTargetType(Type nodeType)
    {
        if (TargetType != null)
            return TargetType.IsAssignableFrom(nodeType) ? TargetType : null;
        if (TypeName == "*")
            return nodeType;
        return UiCssRegistry.MatchElement(nodeType, TypeName);
    }

    internal bool MatchesConditions(UiNode node)
    {
        if (Id is not null && !string.Equals(Id, node.StyleId, StringComparison.Ordinal))
            return false;
        foreach (var className in Classes)
        {
            if (!node.Classes.Contains(className))
                return false;
        }

        if (States != UiPseudoStates.None)
        {
            var active = node.GetStylePseudoStatesForMatching();
            if ((States & active) != States)
                return false;
        }

        return true;
    }

    /// <summary>Gets normalized selector text, omitting an unrestricted wildcard when other conditions exist.</summary>
    public string SelectorText
    {
        get
        {
            var builder = new StringBuilder();
            if (HasTypeConstraint || (Classes.Count == 0 && Id is null && States == UiPseudoStates.None))
                builder.Append(TypeName);
            foreach (var className in Classes)
                builder.Append('.').Append(className);
            if (Id is not null)
                builder.Append('#').Append(Id);
            if (States != UiPseudoStates.None)
            {
                if (States.HasFlag(UiPseudoStates.Hovered)) builder.Append(":hover");
                if (States.HasFlag(UiPseudoStates.Focused)) builder.Append(":focus");
                if (States.HasFlag(UiPseudoStates.Pressed)) builder.Append(":pressed");
                if (States.HasFlag(UiPseudoStates.Disabled)) builder.Append(":disabled");
            }

            return builder.ToString();
        }
    }

    private static int CountBits(UiPseudoStates states)
    {
        var value = (int)states;
        var count = 0;
        while (value != 0)
        {
            count += value & 1;
            value >>= 1;
        }

        return count;
    }

    internal static int ComputeTargetTypeDepth(Type type)
    {
        var depth = 1;
        var current = type;
        while (current != typeof(UiNode))
        {
            current = current.BaseType!;
            depth++;
        }

        return depth;
    }
}

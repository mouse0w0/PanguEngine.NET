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
        IReadOnlyList<UiPseudoClass> pseudoClasses,
        int idCount,
        int classAndPseudoCount,
        int targetTypeDepth)
    {
        TargetType = targetType;
        TypeName = typeName;
        Classes = classes;
        Id = id;
        PseudoClasses = pseudoClasses;
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

    /// <summary>
    /// Gets the required pseudo-class identifiers, deduplicated and sorted by ordinal name;
    /// every identifier must be active on the node to match.
    /// </summary>
    public IReadOnlyList<UiPseudoClass> PseudoClasses { get; }

    /// <summary>Gets the id contribution to specificity: 0 or 1.</summary>
    public int IdCount { get; }

    /// <summary>Gets the combined class and pseudo-class contribution to specificity.</summary>
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
    /// <param name="pseudoClasses">
    /// The optional required pseudo-class identifiers; the input is copied, deduplicated and sorted by name.
    /// </param>
    /// <returns>A new immutable selector.</returns>
    /// <exception cref="ArgumentException">Thrown when a class or id is not a valid ASCII identifier.</exception>
    public static UiStyleSelector For<TNode>(
        IEnumerable<string>? classes = null,
        string? id = null,
        IEnumerable<UiPseudoClass>? pseudoClasses = null)
        where TNode : UiNode
        => Create(typeof(TNode), typeof(TNode).Name, classes, id, pseudoClasses);

    /// <summary>Creates a CSS selector with an element name or <c>*</c> for any element type.</summary>
    internal static UiStyleSelector Create(
        string typeName,
        IEnumerable<string>? classes,
        string? id,
        IEnumerable<UiPseudoClass>? pseudoClasses) =>
        Create(null, typeName, classes, id, pseudoClasses);

    private static UiStyleSelector Create(
        Type? targetType,
        string typeName,
        IEnumerable<string>? classes,
        string? id,
        IEnumerable<UiPseudoClass>? pseudoClasses)
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

        var orderedPseudoClasses = new List<UiPseudoClass>();
        if (pseudoClasses is not null)
        {
            foreach (var pseudoClass in pseudoClasses)
            {
                if (!orderedPseudoClasses.Contains(pseudoClass))
                    orderedPseudoClasses.Add(pseudoClass);
            }
        }

        orderedPseudoClasses.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.Name, right.Name));
        return new UiStyleSelector(
            targetType,
            typeName,
            normalizedClasses.AsReadOnly(),
            id,
            orderedPseudoClasses.AsReadOnly(),
            id is null ? 0 : 1,
            normalizedClasses.Count + orderedPseudoClasses.Count,
            targetType is null ? 0 : ComputeTargetTypeDepth(targetType));
    }

    /// <summary>Determines whether the supplied node matches this selector.</summary>
    /// <param name="node">The node to test.</param>
    /// <returns>true when the node type, classes, id and pseudo classes all satisfy the selector.</returns>
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

        foreach (var pseudoClass in PseudoClasses)
        {
            if (!node.HasPseudoClass(pseudoClass))
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
            if (HasTypeConstraint || (Classes.Count == 0 && Id is null && PseudoClasses.Count == 0))
                builder.Append(TypeName);
            foreach (var className in Classes)
                builder.Append('.').Append(className);
            if (Id is not null)
                builder.Append('#').Append(Id);
            foreach (var pseudoClass in PseudoClasses)
                builder.Append(':').Append(pseudoClass.Name);

            return builder.ToString();
        }
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

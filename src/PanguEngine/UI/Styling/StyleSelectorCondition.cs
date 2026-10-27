using System.Globalization;

namespace PanguEngine.UI.Styling;

/// <summary>Provides an immutable id, class, pseudo-class or sibling-position constraint on an element.</summary>
/// <remarks>The default value represents an empty condition that imposes no constraint.</remarks>
public readonly record struct StyleSelectorCondition
{
    private readonly Kind _kind;
    private readonly object? _value;

    /// <summary>Initializes a condition with its matching parameters.</summary>
    private StyleSelectorCondition(Kind kind, object? value)
    {
        _kind = kind;
        _value = value;
    }

    /// <summary>Creates a condition requiring a style id.</summary>
    /// <param name="id">The required ASCII style identifier.</param>
    /// <returns>The id condition.</returns>
    /// <exception cref="ArgumentException">Thrown when the id is not a valid ASCII identifier.</exception>
    public static StyleSelectorCondition Id(string id)
    {
        StyleIdentifier.ThrowIfInvalid(id, nameof(id));
        return new StyleSelectorCondition(Kind.Id, id);
    }

    /// <summary>Creates a condition requiring membership in a style class.</summary>
    /// <param name="name">The required ASCII class name.</param>
    /// <returns>The class condition.</returns>
    /// <exception cref="ArgumentException">Thrown when the name is not a valid ASCII identifier.</exception>
    public static StyleSelectorCondition Class(string name)
    {
        StyleIdentifier.ThrowIfInvalid(name, nameof(name));
        return new StyleSelectorCondition(Kind.Class, name);
    }

    /// <summary>Creates a condition requiring a named state or structural pseudo class.</summary>
    /// <param name="identifier">The required shared pseudo-class identifier.</param>
    /// <returns>The pseudo-class condition.</returns>
    public static StyleSelectorCondition Pseudo(PseudoClass identifier) => new(Kind.Pseudo, identifier);

    /// <summary>Creates a condition requiring a sibling position in the sequence <c>An+B</c>.</summary>
    /// <param name="fromEnd">Whether positions are counted from the last sibling.</param>
    /// <param name="a">The coefficient of the non-negative sequence index.</param>
    /// <param name="b">The sequence offset.</param>
    /// <returns>The sibling-position condition.</returns>
    public static StyleSelectorCondition Nth(bool fromEnd, int a, int b) =>
        new(fromEnd ? Kind.NthLastChild : Kind.NthChild, new NthData(a, b));

    /// <summary>Gets whether this condition imposes no constraint.</summary>
    internal bool IsEmpty => _kind == Kind.Empty;

    /// <summary>Gets this condition's id contribution to specificity.</summary>
    public int IdCount => _kind == Kind.Id ? 1 : 0;

    /// <summary>Gets this condition's class and pseudo-class contribution to specificity.</summary>
    public int ClassAndPseudoCount =>
        _kind is Kind.Class or Kind.Pseudo or Kind.NthChild or Kind.NthLastChild ? 1 : 0;

    /// <summary>Gets the normalized CSS condition text, including its leading symbol, or an empty string for an empty condition.</summary>
    public string Text => _kind switch
    {
        Kind.Id => "#" + (string)_value!,
        Kind.Class => "." + (string)_value!,
        Kind.Pseudo => ":" + ((PseudoClass)_value!).Name,
        Kind.NthChild or Kind.NthLastChild => ":" + NthName + "(" + (NthData)_value! + ")",
        _ => string.Empty
    };

    /// <summary>Gets the structural changes that can affect this condition's match.</summary>
    public StyleSelectorDependencies Dependencies => _kind switch
    {
        Kind.Pseudo => GetPseudoDependencies((PseudoClass)_value!),
        Kind.NthChild or Kind.NthLastChild => StyleSelectorDependencies.ChildPosition,
        _ => StyleSelectorDependencies.None
    };

    /// <summary>Gets the comparer used to order conditions in normalized selector text.</summary>
    internal static IComparer<StyleSelectorCondition> NormalizationComparer { get; } =
        Comparer<StyleSelectorCondition>.Create(CompareForNormalization);

    /// <summary>Determines whether the supplied element satisfies this condition.</summary>
    /// <param name="node">The element to match.</param>
    /// <returns>true when the element matches or the condition is empty; otherwise false.</returns>
    public bool Matches(UIElement node) => _kind switch
    {
        Kind.Id => string.Equals((string)_value!, node.StyleId, StringComparison.Ordinal),
        Kind.Class => node.Classes.Contains((string)_value!),
        Kind.Pseudo => node.HasPseudoClass((PseudoClass)_value!),
        Kind.NthChild => ((NthData)_value!).Matches(node.GetChildPosition(false)),
        Kind.NthLastChild => ((NthData)_value!).Matches(node.GetChildPosition(true)),
        _ => true
    };

    /// <summary>Gets the CSS function name for this sibling-position direction.</summary>
    private string NthName => _kind == Kind.NthLastChild ? "nth-last-child" : "nth-child";

    /// <summary>Gets the structural dependencies of a named pseudo class.</summary>
    private static StyleSelectorDependencies GetPseudoDependencies(PseudoClass identifier) =>
        identifier == PseudoClass.Root ? StyleSelectorDependencies.Root :
        identifier == PseudoClass.Empty ? StyleSelectorDependencies.Children :
        identifier.IsChildIndexed ? StyleSelectorDependencies.ChildPosition : StyleSelectorDependencies.None;

    /// <summary>Compares conditions in normalized selector order.</summary>
    private static int CompareForNormalization(StyleSelectorCondition left, StyleSelectorCondition right)
    {
        var comparison = GetNormalizationGroup(left._kind).CompareTo(GetNormalizationGroup(right._kind));
        if (comparison != 0)
            return comparison;
        if (left._kind == Kind.Pseudo)
            return StringComparer.Ordinal.Compare(((PseudoClass)left._value!).Name, ((PseudoClass)right._value!).Name);
        if (left._kind is Kind.NthChild or Kind.NthLastChild)
        {
            comparison = StringComparer.Ordinal.Compare(left.NthName, right.NthName);
            if (comparison != 0)
                return comparison;
            var leftData = (NthData)left._value!;
            var rightData = (NthData)right._value!;
            comparison = leftData.A.CompareTo(rightData.A);
            return comparison != 0 ? comparison : leftData.B.CompareTo(rightData.B);
        }

        return 0;
    }

    /// <summary>Gets the normalized ordering group for a condition kind.</summary>
    private static int GetNormalizationGroup(Kind kind) => kind switch
    {
        Kind.Class => 0,
        Kind.Id => 1,
        Kind.Pseudo => 2,
        Kind.NthChild or Kind.NthLastChild => 3,
        _ => -1
    };

    /// <summary>Represents an immutable integer position sequence with a non-negative sequence index.</summary>
    private sealed record NthData
    {
        /// <summary>Initializes the sibling-position sequence parameters.</summary>
        public NthData(int a, int b)
        {
            A = a;
            B = b;
        }

        /// <summary>Gets the coefficient of the non-negative sequence index.</summary>
        public int A { get; }

        /// <summary>Gets the sequence offset.</summary>
        public int B { get; }

        /// <summary>Determines whether the supplied one-based position belongs to this sequence.</summary>
        public bool Matches(int position)
        {
            if (A == 0)
                return position == B;
            var difference = (long)position - B;
            return difference % A == 0 && difference / A >= 0;
        }

        /// <summary>Returns the normalized CSS representation of this sequence.</summary>
        public override string ToString()
        {
            if (A == 0)
                return B.ToString(CultureInfo.InvariantCulture);
            var coefficient = A switch
            {
                1 => "n",
                -1 => "-n",
                _ => A.ToString(CultureInfo.InvariantCulture) + "n"
            };
            return B == 0 ? coefficient : coefficient +
                (B > 0 ? "+" : "") + B.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Identifies the constraint represented by a condition.</summary>
    private enum Kind
    {
        /// <summary>Imposes no constraint.</summary>
        Empty,

        /// <summary>Requires a style id.</summary>
        Id,

        /// <summary>Requires style class membership.</summary>
        Class,

        /// <summary>Requires a named pseudo class.</summary>
        Pseudo,

        /// <summary>Requires a sibling position counted from the first sibling.</summary>
        NthChild,

        /// <summary>Requires a sibling position counted from the last sibling.</summary>
        NthLastChild
    }
}

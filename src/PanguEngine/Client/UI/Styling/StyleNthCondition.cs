namespace PanguEngine.Client.UI.Styling;

/// <summary>Constrains a node's one-based sibling position in the selected direction.</summary>
internal readonly record struct StyleNthCondition(bool FromEnd, NthExpression Expression)
{
    /// <summary>Gets the CSS function name for this direction.</summary>
    internal string Name => FromEnd ? "nth-last-child" : "nth-child";

    /// <summary>Determines whether the node's current position satisfies this condition.</summary>
    internal bool Matches(Node node) => Expression.Matches(node.GetChildPosition(FromEnd));

    /// <summary>Returns the normalized CSS function without its leading colon.</summary>
    public override string ToString() => Name + "(" + Expression + ")";
}

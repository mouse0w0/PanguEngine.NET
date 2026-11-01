namespace PanguEngine.UI.Controls;

/// <summary>Describes a committed change to a tree node's expansion.</summary>
/// <typeparam name="T">The business node type.</typeparam>
public sealed class TreeNodeExpansionChangedEventArgs<T>(T item, bool isExpanded) : EventArgs where T : class
{
    /// <summary>Gets the business node.</summary>
    public T Item { get; } = item;
    /// <summary>Gets whether the node is expanded.</summary>
    public bool IsExpanded { get; } = isExpanded;
}

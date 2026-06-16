using System.Collections.ObjectModel;

namespace PanguEngine.Collections;

/// <summary>
/// Represents the result of a topological sort attempt.
/// </summary>
/// <typeparam name="T">The graph node type.</typeparam>
public sealed class TopologicalSortResult<T> where T : notnull
{
    internal TopologicalSortResult(
        bool success,
        IReadOnlyList<T> orderedNodes,
        IReadOnlyList<IReadOnlyList<T>> layers,
        IReadOnlyList<T> remainingNodes)
    {
        Success = success;
        OrderedNodes = Array.AsReadOnly(orderedNodes.ToArray());
        Layers = CreateLayers(layers);
        RemainingNodes = Array.AsReadOnly(remainingNodes.ToArray());
    }

    /// <summary>
    /// Gets whether every node was sorted successfully.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Gets the ordered nodes produced by the sort attempt.
    /// </summary>
    public IReadOnlyList<T> OrderedNodes { get; }

    /// <summary>
    /// Gets the completed topological layers produced by the sort attempt.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<T>> Layers { get; }

    /// <summary>
    /// Gets nodes that could not be sorted.
    /// </summary>
    public IReadOnlyList<T> RemainingNodes { get; }

    private static ReadOnlyCollection<IReadOnlyList<T>> CreateLayers(IReadOnlyList<IReadOnlyList<T>> layers)
    {
        var copy = new IReadOnlyList<T>[layers.Count];
        for (var i = 0; i < layers.Count; i++)
            copy[i] = Array.AsReadOnly(layers[i].ToArray());

        return Array.AsReadOnly(copy);
    }
}
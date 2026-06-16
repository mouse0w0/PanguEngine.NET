namespace PanguEngine.Collections;

public sealed partial class DirectedGraph<T> where T : notnull
{
    /// <summary>
    /// Sorts the graph nodes topologically.
    /// </summary>
    /// <returns>The sorted nodes.</returns>
    public IReadOnlyList<T> TopologicalSort()
    {
        if (TryTopologicalSort(out var result))
            return result.OrderedNodes;

        throw new InvalidOperationException("Graph contains a cycle.");
    }

    /// <summary>
    /// Attempts to sort the graph nodes topologically.
    /// </summary>
    /// <param name="result">The sort result.</param>
    /// <returns>True when every node was sorted; otherwise, false.</returns>
    public bool TryTopologicalSort(out TopologicalSortResult<T> result)
    {
        var indegrees = CreateIndegrees();
        var processed = new HashSet<T>(_comparer);
        var ordered = new List<T>(_nodes.Count);
        var layers = new List<IReadOnlyList<T>>();
        var currentLayer = GetZeroIndegreeNodes(indegrees, processed);

        while (currentLayer.Count > 0)
        {
            layers.Add(Array.AsReadOnly(currentLayer.ToArray()));

            foreach (var node in currentLayer)
            {
                processed.Add(node);
                ordered.Add(node);

                foreach (var successor in _successors[node])
                    indegrees[successor]--;
            }

            currentLayer = GetZeroIndegreeNodes(indegrees, processed);
        }

        var remaining = GetRemainingNodes(processed);
        var success = remaining.Count == 0;
        result = new TopologicalSortResult<T>(success, ordered, layers, remaining);
        return success;
    }

    /// <summary>
    /// Sorts the graph nodes into topological layers.
    /// </summary>
    /// <returns>The sorted layers.</returns>
    public IReadOnlyList<IReadOnlyList<T>> TopologicalSortLayers()
    {
        if (TryTopologicalSort(out var result))
            return result.Layers;

        throw new InvalidOperationException("Graph contains a cycle.");
    }

    private Dictionary<T, int> CreateIndegrees()
    {
        var indegrees = new Dictionary<T, int>(_comparer);
        foreach (var node in _nodes)
            indegrees.Add(node, _predecessors[node].Count);

        return indegrees;
    }

    private List<T> GetZeroIndegreeNodes(Dictionary<T, int> indegrees, HashSet<T> processed)
    {
        var nodes = new List<T>();
        foreach (var node in _nodes)
        {
            if (!processed.Contains(node) && indegrees[node] == 0)
                nodes.Add(node);
        }

        return nodes;
    }

    private List<T> GetRemainingNodes(HashSet<T> processed)
    {
        var nodes = new List<T>();
        foreach (var node in _nodes)
        {
            if (!processed.Contains(node))
                nodes.Add(node);
        }

        return nodes;
    }
}
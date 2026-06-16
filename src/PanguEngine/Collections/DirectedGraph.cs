using System.Collections.ObjectModel;

namespace PanguEngine.Collections;

/// <summary>
/// Represents a mutable directed graph.
/// </summary>
/// <typeparam name="T">The graph node type.</typeparam>
public sealed partial class DirectedGraph<T> where T : notnull
{
    private static readonly ReadOnlyCollection<T> EmptyNodes = Array.AsReadOnly<T>([]);

    private readonly IEqualityComparer<T> _comparer;
    private readonly Dictionary<T, HashSet<T>> _successors;
    private readonly Dictionary<T, HashSet<T>> _predecessors;
    private readonly List<T> _nodes = [];
    private readonly List<DirectedGraphEdge<T>> _edges = [];
    private readonly ReadOnlyCollection<T> _readOnlyNodes;
    private readonly ReadOnlyCollection<DirectedGraphEdge<T>> _readOnlyEdges;

    /// <summary>
    /// Creates a directed graph that uses the default equality comparer.
    /// </summary>
    public DirectedGraph() : this(null)
    {
    }

    /// <summary>
    /// Creates a directed graph that uses the specified equality comparer.
    /// </summary>
    /// <param name="comparer">The equality comparer used for nodes.</param>
    public DirectedGraph(IEqualityComparer<T>? comparer)
    {
        _comparer = comparer ?? EqualityComparer<T>.Default;
        _successors = new Dictionary<T, HashSet<T>>(_comparer);
        _predecessors = new Dictionary<T, HashSet<T>>(_comparer);
        _readOnlyNodes = _nodes.AsReadOnly();
        _readOnlyEdges = _edges.AsReadOnly();
    }

    /// <summary>
    /// Gets the number of nodes in the graph.
    /// </summary>
    public int Count => _nodes.Count;

    /// <summary>
    /// Gets the number of edges in the graph.
    /// </summary>
    public int EdgeCount => _edges.Count;

    /// <summary>
    /// Gets the graph nodes in insertion order.
    /// </summary>
    public IReadOnlyList<T> Nodes => _readOnlyNodes;

    /// <summary>
    /// Gets the graph edges in insertion order.
    /// </summary>
    public IReadOnlyList<DirectedGraphEdge<T>> Edges => _readOnlyEdges;

    /// <summary>
    /// Adds a node to the graph.
    /// </summary>
    /// <param name="node">The node to add.</param>
    /// <returns>True when the node was added; otherwise, false.</returns>
    public bool AddNode(T node)
    {
        ValidateNode(node, nameof(node));
        return AddNodeUnchecked(node);
    }

    /// <summary>
    /// Adds a directed edge to the graph.
    /// </summary>
    /// <param name="from">The source node.</param>
    /// <param name="to">The target node.</param>
    /// <returns>True when the edge was added; otherwise, false.</returns>
    public bool AddEdge(T from, T to)
    {
        ValidateNode(from, nameof(from));
        ValidateNode(to, nameof(to));
        AddNodeUnchecked(from);
        AddNodeUnchecked(to);

        if (!_successors[from].Add(to))
            return false;

        _predecessors[to].Add(from);
        _edges.Add(new DirectedGraphEdge<T>(from, to));
        return true;
    }

    /// <summary>
    /// Gets whether the graph contains a node.
    /// </summary>
    /// <param name="node">The node to inspect.</param>
    /// <returns>True when the node exists; otherwise, false.</returns>
    public bool ContainsNode(T node)
    {
        ValidateNode(node, nameof(node));
        return _successors.ContainsKey(node);
    }

    /// <summary>
    /// Gets whether the graph contains a directed edge.
    /// </summary>
    /// <param name="from">The source node.</param>
    /// <param name="to">The target node.</param>
    /// <returns>True when the edge exists; otherwise, false.</returns>
    public bool ContainsEdge(T from, T to)
    {
        ValidateNode(from, nameof(from));
        ValidateNode(to, nameof(to));
        return _successors.TryGetValue(from, out var successors) && successors.Contains(to);
    }

    /// <summary>
    /// Removes a node and all attached edges from the graph.
    /// </summary>
    /// <param name="node">The node to remove.</param>
    /// <returns>True when the node was removed; otherwise, false.</returns>
    public bool RemoveNode(T node)
    {
        ValidateNode(node, nameof(node));
        if (!_successors.TryGetValue(node, out var successors) ||
            !_predecessors.TryGetValue(node, out var predecessors))
            return false;

        foreach (var predecessor in predecessors.ToArray())
            RemoveEdge(predecessor, node);

        foreach (var successor in successors.ToArray())
            RemoveEdge(node, successor);

        _successors.Remove(node);
        _predecessors.Remove(node);
        _nodes.RemoveAll(current => _comparer.Equals(current, node));
        return true;
    }

    /// <summary>
    /// Removes a directed edge from the graph.
    /// </summary>
    /// <param name="from">The source node.</param>
    /// <param name="to">The target node.</param>
    /// <returns>True when the edge was removed; otherwise, false.</returns>
    public bool RemoveEdge(T from, T to)
    {
        ValidateNode(from, nameof(from));
        ValidateNode(to, nameof(to));
        if (!_successors.TryGetValue(from, out var successors) || !successors.Remove(to))
            return false;

        _predecessors[to].Remove(from);
        _edges.RemoveAll(edge => _comparer.Equals(edge.From, from) && _comparer.Equals(edge.To, to));
        return true;
    }

    /// <summary>
    /// Removes all nodes and edges from the graph.
    /// </summary>
    public void Clear()
    {
        _successors.Clear();
        _predecessors.Clear();
        _nodes.Clear();
        _edges.Clear();
    }

    /// <summary>
    /// Gets the successor nodes of a node.
    /// </summary>
    /// <param name="node">The node to inspect.</param>
    /// <returns>The successor nodes.</returns>
    public IReadOnlyList<T> GetSuccessors(T node)
    {
        ValidateNode(node, nameof(node));
        return _successors.TryGetValue(node, out var successors) ? GetNodesInInsertionOrder(successors) : EmptyNodes;
    }

    /// <summary>
    /// Gets the predecessor nodes of a node.
    /// </summary>
    /// <param name="node">The node to inspect.</param>
    /// <returns>The predecessor nodes.</returns>
    public IReadOnlyList<T> GetPredecessors(T node)
    {
        ValidateNode(node, nameof(node));
        return _predecessors.TryGetValue(node, out var predecessors)
            ? GetNodesInInsertionOrder(predecessors)
            : EmptyNodes;
    }

    private bool AddNodeUnchecked(T node)
    {
        if (_successors.ContainsKey(node))
            return false;

        _successors.Add(node, new HashSet<T>(_comparer));
        _predecessors.Add(node, new HashSet<T>(_comparer));
        _nodes.Add(node);
        return true;
    }

    private ReadOnlyCollection<T> GetNodesInInsertionOrder(HashSet<T> nodes)
    {
        var ordered = new List<T>(nodes.Count);
        foreach (var node in _nodes)
        {
            if (nodes.Contains(node))
                ordered.Add(node);
        }

        return ordered.AsReadOnly();
    }

    private static void ValidateNode(T node, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(node, parameterName);
    }
}
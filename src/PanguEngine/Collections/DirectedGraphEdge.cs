namespace PanguEngine.Collections;

/// <summary>
/// Represents a directed edge between two graph nodes.
/// </summary>
/// <typeparam name="T">The graph node type.</typeparam>
/// <param name="From">The source node.</param>
/// <param name="To">The target node.</param>
public readonly record struct DirectedGraphEdge<T>(T From, T To) where T : notnull;
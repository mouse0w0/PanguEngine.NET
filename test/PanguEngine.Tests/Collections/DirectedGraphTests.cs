using PanguEngine.Collections;

namespace PanguEngine.Tests.Collections;

public sealed class DirectedGraphTests
{
    [Fact]
    public void TopologicalSortReturnsEmptyListForEmptyGraph()
    {
        var graph = new DirectedGraph<string>();

        var ordered = graph.TopologicalSort();

        Assert.Empty(ordered);
    }

    [Fact]
    public void TopologicalSortReturnsSingleNode()
    {
        var graph = new DirectedGraph<string>();
        graph.AddNode("stone");

        var ordered = graph.TopologicalSort();

        Assert.Equal(["stone"], ordered);
    }

    [Fact]
    public void TopologicalSortUsesEdgeDirection()
    {
        var graph = new DirectedGraph<string>();
        graph.AddEdge("load", "render");

        var ordered = graph.TopologicalSort();

        Assert.Equal(["load", "render"], ordered);
    }

    [Fact]
    public void TopologicalSortKeepsStableInsertionOrderForIndependentNodes()
    {
        var graph = new DirectedGraph<string>();
        graph.AddNode("third");
        graph.AddNode("first");
        graph.AddNode("second");

        var ordered = graph.TopologicalSort();

        Assert.Equal(["third", "first", "second"], ordered);
    }

    [Fact]
    public void TopologicalSortLayersGroupsCurrentZeroIndegreeNodes()
    {
        var graph = new DirectedGraph<string>();
        graph.AddNode("load-texture");
        graph.AddNode("load-mesh");
        graph.AddEdge("load-texture", "render");
        graph.AddEdge("load-mesh", "render");

        var layers = graph.TopologicalSortLayers();

        Assert.Collection(
            layers,
            layer => Assert.Equal(["load-texture", "load-mesh"], layer),
            layer => Assert.Equal(["render"], layer));
    }

    [Fact]
    public void TryTopologicalSortReturnsRemainingNodesForCycle()
    {
        var graph = new DirectedGraph<string>();
        graph.AddEdge("a", "b");
        graph.AddEdge("b", "c");
        graph.AddEdge("c", "b");

        var sorted = graph.TryTopologicalSort(out var result);

        Assert.False(sorted);
        Assert.False(result.Success);
        Assert.Equal(["a"], result.OrderedNodes);
        Assert.Equal(["b", "c"], result.RemainingNodes);
    }

    [Fact]
    public void FindCyclicComponentsReturnsCyclicComponents()
    {
        var graph = new DirectedGraph<string>();
        graph.AddEdge("a", "b");
        graph.AddEdge("b", "c");
        graph.AddEdge("c", "b");
        graph.AddEdge("c", "d");

        var components = graph.FindCyclicComponents();

        Assert.Collection(
            components,
            cycle => Assert.Equal(["b", "c"], cycle));
    }

    [Fact]
    public void FindCyclicComponentsReturnsSelfLoopAsSingleNodeComponent()
    {
        var graph = new DirectedGraph<string>();
        graph.AddEdge("self", "self");

        var components = graph.FindCyclicComponents();

        Assert.Collection(
            components,
            cycle => Assert.Equal(["self"], cycle));
    }

    [Fact]
    public void FindCyclicComponentsReturnsMultipleComponentsInInsertionOrder()
    {
        var graph = new DirectedGraph<string>();
        graph.AddEdge("a", "b");
        graph.AddEdge("b", "a");
        graph.AddEdge("c", "d");
        graph.AddEdge("d", "c");

        var components = graph.FindCyclicComponents();

        Assert.Collection(
            components,
            cycle => Assert.Equal(["a", "b"], cycle),
            cycle => Assert.Equal(["c", "d"], cycle));
    }

    [Fact]
    public void FindCyclicComponentsReturnsEmptyListForAcyclicGraph()
    {
        var graph = new DirectedGraph<string>();
        graph.AddEdge("a", "b");

        var components = graph.FindCyclicComponents();

        Assert.Empty(components);
    }

    [Fact]
    public void StrictTopologicalSortRejectsSelfLoop()
    {
        var graph = new DirectedGraph<string>();
        graph.AddEdge("self", "self");

        Assert.Throws<InvalidOperationException>(() => graph.TopologicalSort());
        Assert.Throws<InvalidOperationException>(() => graph.TopologicalSortLayers());
    }

    [Fact]
    public void AddEdgeAddsMissingNodesAndExposesEdges()
    {
        var graph = new DirectedGraph<string>();

        var added = graph.AddEdge("prepare", "execute");

        Assert.True(added);
        Assert.Equal(2, graph.Count);
        Assert.Equal(1, graph.EdgeCount);
        Assert.Equal(["prepare", "execute"], graph.Nodes);
        Assert.Equal([new DirectedGraphEdge<string>("prepare", "execute")], graph.Edges);
        Assert.True(graph.ContainsNode("prepare"));
        Assert.True(graph.ContainsEdge("prepare", "execute"));
    }

    [Fact]
    public void AddNodeAndAddEdgeReturnFalseForDuplicates()
    {
        var graph = new DirectedGraph<string>();

        Assert.True(graph.AddNode("node"));
        Assert.False(graph.AddNode("node"));
        Assert.True(graph.AddEdge("node", "other"));
        Assert.False(graph.AddEdge("node", "other"));
        Assert.Equal(2, graph.Count);
        Assert.Equal(1, graph.EdgeCount);
    }

    [Fact]
    public void GetSuccessorsAndPredecessorsUseNodeInsertionOrder()
    {
        var graph = new DirectedGraph<string>();
        graph.AddNode("second");
        graph.AddNode("first");
        graph.AddEdge("root", "first");
        graph.AddEdge("root", "second");

        Assert.Equal(["second", "first"], graph.GetSuccessors("root"));
        Assert.Equal(["root"], graph.GetPredecessors("first"));
        Assert.Empty(graph.GetSuccessors("missing"));
        Assert.Empty(graph.GetPredecessors("missing"));
    }

    [Fact]
    public void RemoveEdgeUpdatesGraph()
    {
        var graph = new DirectedGraph<string>();
        graph.AddEdge("a", "b");

        var removed = graph.RemoveEdge("a", "b");

        Assert.True(removed);
        Assert.False(graph.RemoveEdge("a", "b"));
        Assert.False(graph.ContainsEdge("a", "b"));
        Assert.Equal(0, graph.EdgeCount);
        Assert.Equal(["a", "b"], graph.TopologicalSort());
    }

    [Fact]
    public void RemoveNodeRemovesAttachedEdges()
    {
        var graph = new DirectedGraph<string>();
        graph.AddEdge("a", "b");
        graph.AddEdge("b", "c");

        var removed = graph.RemoveNode("b");

        Assert.True(removed);
        Assert.False(graph.RemoveNode("b"));
        Assert.Equal(["a", "c"], graph.Nodes);
        Assert.Empty(graph.Edges);
        Assert.False(graph.ContainsNode("b"));
        Assert.False(graph.ContainsEdge("a", "b"));
        Assert.False(graph.ContainsEdge("b", "c"));
    }

    [Fact]
    public void ClearRemovesNodesAndEdges()
    {
        var graph = new DirectedGraph<string>();
        graph.AddEdge("a", "b");

        graph.Clear();

        Assert.Equal(0, graph.Count);
        Assert.Equal(0, graph.EdgeCount);
        Assert.Empty(graph.Nodes);
        Assert.Empty(graph.Edges);
    }

    [Fact]
    public void GraphUsesProvidedComparer()
    {
        var graph = new DirectedGraph<string>(StringComparer.OrdinalIgnoreCase);

        Assert.True(graph.AddNode("Node"));
        Assert.False(graph.AddNode("node"));
        Assert.True(graph.ContainsNode("NODE"));
        Assert.True(graph.AddEdge("Node", "Next"));
        Assert.True(graph.ContainsEdge("node", "next"));
    }

    [Fact]
    public void NodeApisRejectNullReferences()
    {
        var graph = new DirectedGraph<string>();

        Assert.Throws<ArgumentNullException>(() => graph.AddNode(null!));
        Assert.Throws<ArgumentNullException>(() => graph.AddEdge(null!, "node"));
        Assert.Throws<ArgumentNullException>(() => graph.AddEdge("node", null!));
        Assert.Throws<ArgumentNullException>(() => graph.ContainsNode(null!));
        Assert.Throws<ArgumentNullException>(() => graph.ContainsEdge(null!, "node"));
        Assert.Throws<ArgumentNullException>(() => graph.ContainsEdge("node", null!));
        Assert.Throws<ArgumentNullException>(() => graph.RemoveNode(null!));
        Assert.Throws<ArgumentNullException>(() => graph.RemoveEdge(null!, "node"));
        Assert.Throws<ArgumentNullException>(() => graph.RemoveEdge("node", null!));
        Assert.Throws<ArgumentNullException>(() => graph.GetSuccessors(null!));
        Assert.Throws<ArgumentNullException>(() => graph.GetPredecessors(null!));
    }
}
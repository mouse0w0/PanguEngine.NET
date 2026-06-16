namespace PanguEngine.Collections;

public sealed partial class DirectedGraph<T> where T : notnull
{
    /// <summary>
    /// Finds strongly connected components that contain at least one cycle.
    /// </summary>
    /// <returns>The cyclic components in node insertion order.</returns>
    public IReadOnlyList<IReadOnlyList<T>> FindCyclicComponents()
    {
        if (_nodes.Count == 0)
            return [];

        var nodeOrder = CreateNodeOrderMap();
        var index = 0;
        var indexes = new Dictionary<T, int>(_comparer);
        var lowLinks = new Dictionary<T, int>(_comparer);
        var stack = new Stack<T>();
        var stacked = new HashSet<T>(_comparer);
        var components = new List<IReadOnlyList<T>>();

        foreach (var node in _nodes)
        {
            if (!indexes.TryGetValue(node, out _))
                ConnectCyclicComponent(node, ref index, indexes, lowLinks, stack, stacked, components, nodeOrder);
        }

        components.Sort((left, right) => nodeOrder[left[0]].CompareTo(nodeOrder[right[0]]));
        return components.AsReadOnly();
    }

    private void ConnectCyclicComponent(
        T root,
        ref int index,
        Dictionary<T, int> indexes,
        Dictionary<T, int> lowLinks,
        Stack<T> stack,
        HashSet<T> stacked,
        List<IReadOnlyList<T>> components,
        Dictionary<T, int> nodeOrder)
    {
        var frames = new Stack<CyclicComponentFrame>();
        VisitCyclicComponentNode(root, ref index, indexes, lowLinks, stack, stacked, frames);

        while (frames.Count > 0)
        {
            var frame = frames.Pop();
            if (frame.HasReturnedSuccessor)
            {
                lowLinks[frame.Node] = Math.Min(lowLinks[frame.Node], lowLinks[frame.ReturnedSuccessor]);
                frame.HasReturnedSuccessor = false;
            }

            var advanced = false;
            while (frame.Successors.MoveNext())
            {
                var successor = frame.Successors.Current;
                if (!indexes.TryGetValue(successor, out var successorIndex))
                {
                    frame.ReturnedSuccessor = successor;
                    frame.HasReturnedSuccessor = true;
                    frames.Push(frame);
                    VisitCyclicComponentNode(successor, ref index, indexes, lowLinks, stack, stacked, frames);
                    advanced = true;
                    break;
                }

                if (stacked.Contains(successor))
                    lowLinks[frame.Node] = Math.Min(lowLinks[frame.Node], successorIndex);
            }

            if (advanced)
                continue;

            if (lowLinks[frame.Node] == indexes[frame.Node])
                AddCyclicComponent(frame.Node, stack, stacked, components, nodeOrder);
        }
    }

    private void VisitCyclicComponentNode(
        T node,
        ref int index,
        Dictionary<T, int> indexes,
        Dictionary<T, int> lowLinks,
        Stack<T> stack,
        HashSet<T> stacked,
        Stack<CyclicComponentFrame> frames)
    {
        indexes.Add(node, index);
        lowLinks.Add(node, index);
        index++;
        stack.Push(node);
        stacked.Add(node);
        frames.Push(new CyclicComponentFrame(node, _successors[node].GetEnumerator()));
    }

    private void AddCyclicComponent(
        T node,
        Stack<T> stack,
        HashSet<T> stacked,
        List<IReadOnlyList<T>> components,
        Dictionary<T, int> nodeOrder)
    {
        var component = new List<T>();
        T current;
        do
        {
            current = stack.Pop();
            stacked.Remove(current);
            component.Add(current);
        } while (!_comparer.Equals(current, node));

        component.Sort((left, right) => nodeOrder[left].CompareTo(nodeOrder[right]));
        if (component.Count > 1 || _successors[node].Contains(node))
            components.Add(component.AsReadOnly());
    }

    private Dictionary<T, int> CreateNodeOrderMap()
    {
        var nodeOrder = new Dictionary<T, int>(_comparer);
        for (var i = 0; i < _nodes.Count; i++)
            nodeOrder.Add(_nodes[i], i);

        return nodeOrder;
    }

    private struct CyclicComponentFrame(T node, HashSet<T>.Enumerator successors)
    {
        public readonly T Node = node;

        public HashSet<T>.Enumerator Successors = successors;

        public bool HasReturnedSuccessor;

        public T ReturnedSuccessor = default!;
    }
}
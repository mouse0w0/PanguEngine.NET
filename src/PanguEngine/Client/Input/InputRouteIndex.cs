using PanguEngine.Input;
using Silk.NET.Maths;

namespace PanguEngine.Client.Input;

internal sealed record InputRouteCandidate(
    InputAction Action,
    InputBindingEntry[] Bindings,
    Vector3D<double> Scale,
    int Priority,
    int Order);

internal sealed class InputRouteIndex
{
    private static readonly InputRouteCandidate[] EmptyCandidates = [];
    private readonly Dictionary<InputSource, SourceRoutes> _routesBySource;

    private InputRouteIndex(Dictionary<InputSource, SourceRoutes> routesBySource) =>
        _routesBySource = routesBySource;

    internal static InputRouteIndex Create(IReadOnlyList<InputBindingEntry> entries)
    {
        var groups = new Dictionary<
            InputSource,
            Dictionary<InputContext, Dictionary<InputAction, List<InputBindingEntry>>>>();
        foreach (var entry in entries)
        {
            if (!entry.IsEnabled)
                continue;

            if (!groups.TryGetValue(entry.Source, out var contextGroups))
            {
                contextGroups = [];
                groups.Add(entry.Source, contextGroups);
            }
            if (!contextGroups.TryGetValue(entry.Context, out var actionGroups))
            {
                actionGroups = [];
                contextGroups.Add(entry.Context, actionGroups);
            }
            if (!actionGroups.TryGetValue(entry.Action, out var actionEntries))
            {
                actionEntries = [];
                actionGroups.Add(entry.Action, actionEntries);
            }
            actionEntries.Add(entry);
        }

        var routesBySource = new Dictionary<InputSource, SourceRoutes>();
        foreach (var sourceGroup in groups)
        {
            var routesByContext = new Dictionary<InputContext, ContextRoutes>();
            foreach (var contextGroup in sourceGroup.Value)
            {
                var fallback = CreateCandidates(contextGroup.Value, KeyModifiers.None);
                var exactModifiers = new HashSet<KeyModifiers>();
                foreach (var actionEntries in contextGroup.Value.Values)
                {
                    foreach (var entry in actionEntries)
                    {
                        if (entry.Modifiers != KeyModifiers.None)
                            exactModifiers.Add(entry.Modifiers);
                    }
                }

                var exactRoutes = new Dictionary<KeyModifiers, InputRouteCandidate[]>();
                foreach (var modifiers in exactModifiers)
                    exactRoutes.Add(modifiers, CreateCandidates(contextGroup.Value, modifiers));
                routesByContext.Add(contextGroup.Key, new ContextRoutes(fallback, exactRoutes));
            }
            routesBySource.Add(sourceGroup.Key, new SourceRoutes(routesByContext));
        }

        return new InputRouteIndex(routesBySource);
    }

    internal InputRouteCandidate[] GetCandidates(
        InputSource source,
        InputContext context,
        KeyModifiers modifiers)
    {
        if (!_routesBySource.TryGetValue(source, out var sourceRoutes) ||
            !sourceRoutes.ByContext.TryGetValue(context, out var contextRoutes))
        {
            return EmptyCandidates;
        }

        return modifiers != KeyModifiers.None &&
               contextRoutes.ExactRoutes.TryGetValue(modifiers, out var exactCandidates)
            ? exactCandidates
            : contextRoutes.Fallback;
    }

    private static InputRouteCandidate[] CreateCandidates(
        Dictionary<InputAction, List<InputBindingEntry>> actionGroups,
        KeyModifiers modifiers)
    {
        var candidates = new List<InputRouteCandidate>();
        foreach (var actionGroup in actionGroups)
        {
            var selected = SelectEntries(actionGroup.Value, modifiers);
            if (selected.Count == 0)
                continue;

            var scale = Vector3D<double>.Zero;
            var priority = int.MinValue;
            var order = int.MaxValue;
            foreach (var entry in selected)
            {
                scale = new Vector3D<double>(
                    scale.X + entry.Scale.X,
                    scale.Y + entry.Scale.Y,
                    scale.Z + entry.Scale.Z);
                priority = Math.Max(priority, entry.Priority);
                order = Math.Min(order, entry.Order);
            }
            candidates.Add(new InputRouteCandidate(
                actionGroup.Key,
                selected.ToArray(),
                scale,
                priority,
                order));
        }

        candidates.Sort(static (left, right) =>
        {
            var priorityComparison = right.Priority.CompareTo(left.Priority);
            return priorityComparison != 0
                ? priorityComparison
                : left.Order.CompareTo(right.Order);
        });
        return candidates.ToArray();
    }

    private static List<InputBindingEntry> SelectEntries(
        List<InputBindingEntry> entries,
        KeyModifiers modifiers)
    {
        if (modifiers != KeyModifiers.None)
        {
            var exact = entries.Where(entry => entry.Modifiers == modifiers).ToList();
            if (exact.Count != 0)
                return exact;
        }

        return entries.Where(entry => entry.Modifiers == KeyModifiers.None).ToList();
    }

    private sealed class SourceRoutes(Dictionary<InputContext, ContextRoutes> byContext)
    {
        internal Dictionary<InputContext, ContextRoutes> ByContext { get; } = byContext;
    }

    private sealed class ContextRoutes(
        InputRouteCandidate[] fallback,
        Dictionary<KeyModifiers, InputRouteCandidate[]> exactRoutes)
    {
        internal InputRouteCandidate[] Fallback { get; } = fallback;
        internal Dictionary<KeyModifiers, InputRouteCandidate[]> ExactRoutes { get; } = exactRoutes;
    }
}

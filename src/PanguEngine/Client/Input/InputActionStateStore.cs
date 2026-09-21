using PanguEngine.Input;
using PanguEngine.Registries;
using Silk.NET.Maths;

namespace PanguEngine.Client.Input;

internal sealed class InputActionStateStore
{
    private static readonly InputActionEvent[] EmptyEvents = [];
    private readonly Dictionary<ResourceKey, Contribution> _bySlot = [];
    private readonly Dictionary<ResourceKey, Contribution> _exactBySlot = [];
    private readonly Dictionary<InputSource, List<Contribution>> _bySource = [];
    private readonly Dictionary<ActionStateKey, List<Contribution>> _byAction = [];
    private readonly Dictionary<ActionStateKey, InputActionValue> _states = [];

    internal bool Activate(InputRouteCandidate candidate, out InputActionEvent eventArgs)
    {
        var bindings = candidate.Bindings;
        var actionKey = new ActionStateKey(bindings[0].Context, candidate.Action);
        foreach (var binding in bindings)
        {
            if (_bySlot.ContainsKey(binding.Key))
                continue;

            var contribution = new Contribution(
                binding.Key,
                binding.Source,
                actionKey,
                binding.Modifiers,
                CreateContributionValue(candidate.Action, binding.Scale));
            _bySlot.Add(binding.Key, contribution);
            if (binding.Modifiers != KeyModifiers.None)
                _exactBySlot.Add(binding.Key, contribution);
            AddIndex(_bySource, binding.Source, contribution);
            AddIndex(_byAction, actionKey, contribution);
        }

        return TryUpdateState(
            actionKey,
            bindings[0].Source,
            out eventArgs);
    }

    internal IReadOnlyList<InputActionEvent> Release(
        InputSource source,
        KeyModifiers modifiers,
        bool modifiersChanged)
    {
        var targets = new List<Contribution>();
        var targetSlots = new HashSet<ResourceKey>();
        if (_bySource.TryGetValue(source, out var sourceContributions))
        {
            foreach (var contribution in sourceContributions)
            {
                if (targetSlots.Add(contribution.SlotId))
                    targets.Add(contribution);
            }
        }
        if (modifiersChanged)
        {
            foreach (var contribution in _exactBySlot.Values)
            {
                if (contribution.Modifiers != modifiers && targetSlots.Add(contribution.SlotId))
                    targets.Add(contribution);
            }
        }

        return RemoveTargets(targets, source);
    }

    internal IReadOnlyList<InputActionEvent> WithdrawInvalidModifiers(
        KeyModifiers modifiers,
        InputSource eventSource)
    {
        var targets = new List<Contribution>();
        foreach (var contribution in _exactBySlot.Values)
        {
            if (contribution.Modifiers != modifiers)
                targets.Add(contribution);
        }

        return RemoveTargets(targets, eventSource);
    }

    internal IReadOnlyList<InputActionEvent> RemoveBindings(IEnumerable<ResourceKey> slotIds)
    {
        var targets = new List<Contribution>();
        var targetSlots = new HashSet<ResourceKey>();
        foreach (var slotId in slotIds)
        {
            if (targetSlots.Add(slotId) && _bySlot.TryGetValue(slotId, out var contribution))
                targets.Add(contribution);
        }

        return RemoveTargets(targets, null);
    }

    internal IReadOnlyList<InputActionEvent> CancelAll()
    {
        if (_states.Count == 0)
        {
            ClearContributions();
            return EmptyEvents;
        }

        var events = new List<InputActionEvent>(_states.Count);
        foreach (var state in _states)
        {
            events.Add(new InputActionEvent(
                state.Key.Action,
                state.Key.Context,
                InputActionPhase.Stopped,
                InputActionValue.Zero,
                null));
        }
        _states.Clear();
        ClearContributions();
        return events;
    }

    internal bool TryGetValue(
        InputContext context,
        InputAction action,
        out InputActionValue value) =>
        _states.TryGetValue(new ActionStateKey(context, action), out value);

    private IReadOnlyList<InputActionEvent> RemoveTargets(
        List<Contribution> targets,
        InputSource? eventSource)
    {
        if (targets.Count == 0)
            return EmptyEvents;

        var affected = new List<ActionStateKey>();
        var affectedKeys = new HashSet<ActionStateKey>();
        foreach (var contribution in targets)
        {
            RemoveContribution(contribution);
            if (affectedKeys.Add(contribution.ActionKey))
                affected.Add(contribution.ActionKey);
        }

        var events = new List<InputActionEvent>(affected.Count);
        foreach (var key in affected)
        {
            if (TryUpdateState(key, eventSource, out var eventArgs))
                events.Add(eventArgs);
        }
        return events;
    }

    private void RemoveContribution(Contribution contribution)
    {
        _bySlot.Remove(contribution.SlotId);
        _exactBySlot.Remove(contribution.SlotId);
        RemoveIndex(_bySource, contribution.Source, contribution);
        RemoveIndex(_byAction, contribution.ActionKey, contribution);
    }

    private static void AddIndex<TKey>(
        Dictionary<TKey, List<Contribution>> index,
        TKey key,
        Contribution contribution)
        where TKey : notnull
    {
        if (!index.TryGetValue(key, out var contributions))
        {
            contributions = [];
            index.Add(key, contributions);
        }
        contributions.Add(contribution);
    }

    private static void RemoveIndex<TKey>(
        Dictionary<TKey, List<Contribution>> index,
        TKey key,
        Contribution contribution)
        where TKey : notnull
    {
        var contributions = index[key];
        contributions.Remove(contribution);
        if (contributions.Count == 0)
            index.Remove(key);
    }

    private bool TryUpdateState(
        ActionStateKey key,
        InputSource? source,
        out InputActionEvent eventArgs)
    {
        var previous = _states.TryGetValue(key, out var oldValue)
            ? oldValue
            : InputActionValue.Zero;
        var current = Aggregate(key);
        if (previous == current)
        {
            eventArgs = default;
            return false;
        }

        if (current.IsZero)
            _states.Remove(key);
        else
            _states[key] = current;
        var phase = current.IsZero
            ? InputActionPhase.Stopped
            : previous.IsZero
                ? InputActionPhase.Started
                : InputActionPhase.Updated;
        eventArgs = new InputActionEvent(key.Action, key.Context, phase, current, source);
        return true;
    }

    private InputActionValue Aggregate(ActionStateKey key)
    {
        if (!_byAction.TryGetValue(key, out var contributions))
            return InputActionValue.Zero;

        var x = 0d;
        var y = 0d;
        var z = 0d;
        foreach (var contribution in contributions)
        {
            var value = contribution.Value.Axis3D;
            x += value.X;
            y += value.Y;
            z += value.Z;
        }

        return key.Action.ValueType switch
        {
            InputValueType.Button => InputActionValue.FromButton(x != 0),
            InputValueType.Axis1D => InputActionValue.FromAxis1D(Math.Clamp(x, -1, 1)),
            InputValueType.Axis2D => InputActionValue.FromAxis2D(new Vector2D<double>(
                Math.Clamp(x, -1, 1),
                Math.Clamp(y, -1, 1))),
            InputValueType.Axis3D => InputActionValue.FromAxis3D(new Vector3D<double>(
                Math.Clamp(x, -1, 1),
                Math.Clamp(y, -1, 1),
                Math.Clamp(z, -1, 1))),
            _ => throw new InvalidOperationException(
                $"Unsupported action value type '{key.Action.ValueType}'.")
        };
    }

    private static InputActionValue CreateContributionValue(
        InputAction action,
        Vector3D<double> scale) => action.ValueType switch
        {
            InputValueType.Button => InputActionValue.FromButton(true),
            InputValueType.Axis1D => InputActionValue.FromAxis1D(scale.X),
            InputValueType.Axis2D => InputActionValue.FromAxis2D(new Vector2D<double>(scale.X, scale.Y)),
            InputValueType.Axis3D => InputActionValue.FromAxis3D(scale),
            _ => throw new InvalidOperationException(
                $"Unsupported action value type '{action.ValueType}'.")
        };

    private void ClearContributions()
    {
        _bySlot.Clear();
        _exactBySlot.Clear();
        _bySource.Clear();
        _byAction.Clear();
    }

    private readonly record struct ActionStateKey(InputContext Context, InputAction Action);

    private readonly record struct Contribution(
        ResourceKey SlotId,
        InputSource Source,
        ActionStateKey ActionKey,
        KeyModifiers Modifiers,
        InputActionValue Value);
}

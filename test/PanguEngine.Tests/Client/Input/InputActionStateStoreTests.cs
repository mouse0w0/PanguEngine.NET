using PanguEngine.Client.Input;
using PanguEngine.Input;
using PanguEngine.Registries;
using Silk.NET.Maths;

namespace PanguEngine.Tests.Client.Input;

public sealed class InputActionStateStoreTests
{
    [Fact]
    public void ActivateAggregatesAndClampsAxes()
    {
        var context = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Axis3D);
        var store = new InputActionStateStore();

        Assert.True(store.Activate(
            Candidate(
                action,
                context,
                InputSource.FromKey(Key.W),
                KeyModifiers.None,
                ("w", new Vector3D<double>(2, 2, -2))),
            out var started));
        Assert.Equal(new Vector3D<double>(1, 1, -1), started.Value.Axis3D);

        Assert.True(store.Activate(
            Candidate(
                action,
                context,
                InputSource.FromKey(Key.D),
                KeyModifiers.None,
                ("d", new Vector3D<double>(0.5, -2, 0.5))),
            out var updated));
        Assert.Equal(new Vector3D<double>(1, 0, -1), updated.Value.Axis3D);
    }

    [Fact]
    public void ButtonActivationUsesOrAggregation()
    {
        var context = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Button);
        var store = new InputActionStateStore();

        Assert.True(store.Activate(
            Candidate(
                action,
                context,
                InputSource.FromKey(Key.W),
                KeyModifiers.None,
                ("w", Vector3D<double>.Zero)),
            out var started));
        Assert.True(started.Value.Button);

        var events = store.Release(
            InputSource.FromKey(Key.W),
            KeyModifiers.None,
            modifiersChanged: false);

        Assert.Equal(InputActionPhase.Stopped, Assert.Single(events).Phase);
        Assert.False(store.TryGetValue(context, action, out _));
    }

    [Fact]
    public void UnchangedAggregateKeepsContributionWithoutEmittingEvent()
    {
        var context = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Axis1D);
        var store = new InputActionStateStore();

        Assert.True(store.Activate(
            Candidate(
                action,
                context,
                InputSource.FromKey(Key.W),
                KeyModifiers.None,
                ("w", new Vector3D<double>(0.5, 0, 0))),
            out _));
        Assert.False(store.Activate(
            Candidate(
                action,
                context,
                InputSource.FromKey(Key.D),
                KeyModifiers.None,
                ("positive", new Vector3D<double>(0.5, 0, 0)),
                ("negative", new Vector3D<double>(-0.5, 0, 0))),
            out _));

        var events = store.RemoveBindings([ResourceKey.Parse("test:action/negative")]);

        Assert.Equal(1, Assert.Single(events).Value.Axis1D);
    }

    [Fact]
    public void ReleaseAndModifierMismatchProduceOneFinalEvent()
    {
        var context = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Axis1D);
        var store = new InputActionStateStore();
        Assert.True(store.Activate(
            Candidate(
                action,
                context,
                InputSource.FromKey(Key.ShiftLeft),
                KeyModifiers.None,
                ("modifier", new Vector3D<double>(0.5, 0, 0))),
            out _));
        Assert.True(store.Activate(
            Candidate(
                action,
                context,
                InputSource.FromKey(Key.W),
                KeyModifiers.Shift,
                ("exact", new Vector3D<double>(0.5, 0, 0))),
            out _));

        var events = store.Release(
            InputSource.FromKey(Key.ShiftLeft),
            KeyModifiers.None,
            modifiersChanged: true);

        var stopped = Assert.Single(events);
        Assert.Equal(InputActionPhase.Stopped, stopped.Phase);
        Assert.Equal(InputSource.FromKey(Key.ShiftLeft), stopped.Source);
        Assert.False(store.TryGetValue(context, action, out _));
    }

    [Fact]
    public void ModifierMismatchLeavesWildcardContributionActive()
    {
        var context = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Button);
        var store = new InputActionStateStore();
        Assert.True(store.Activate(
            Candidate(
                action,
                context,
                InputSource.FromKey(Key.W),
                KeyModifiers.None,
                ("wildcard", Vector3D<double>.Zero)),
            out _));

        var events = store.WithdrawInvalidModifiers(
            KeyModifiers.Shift,
            InputSource.FromKey(Key.ShiftLeft));

        Assert.Empty(events);
        Assert.True(store.TryGetValue(context, action, out var value));
        Assert.True(value.Button);
    }

    [Fact]
    public void BindingRemovalCoalescesAffectedActionEvents()
    {
        var context = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Axis1D);
        var otherAction = new InputAction(InputValueType.Button);
        var store = new InputActionStateStore();
        Assert.True(store.Activate(
            Candidate(
                action,
                context,
                InputSource.FromKey(Key.W),
                KeyModifiers.None,
                ("first", new Vector3D<double>(0.5, 0, 0)),
                ("second", new Vector3D<double>(0.5, 0, 0))),
            out _));
        Assert.True(store.Activate(
            Candidate(
                otherAction,
                context,
                InputSource.FromKey(Key.D),
                KeyModifiers.None,
                ("other", Vector3D<double>.Zero)),
            out _));

        var events = store.RemoveBindings(
        [
            ResourceKey.Parse("test:action/first"),
            ResourceKey.Parse("test:action/second")
        ]);

        Assert.Single(events);
        Assert.Equal(InputActionPhase.Stopped, events[0].Phase);
        Assert.Null(events[0].Source);
        Assert.True(store.TryGetValue(context, otherAction, out var otherValue));
        Assert.True(otherValue.Button);
    }

    [Fact]
    public void CancelAllClearsStateAndOnlyStopsNonZeroActions()
    {
        var context = new InputContext(InputScope.Game);
        var active = new InputAction(InputValueType.Button);
        var zero = new InputAction(InputValueType.Axis1D);
        var store = new InputActionStateStore();
        Assert.True(store.Activate(
            Candidate(
                active,
                context,
                InputSource.FromKey(Key.W),
                KeyModifiers.None,
                ("active", Vector3D<double>.Zero)),
            out _));
        Assert.False(store.Activate(
            Candidate(
                zero,
                context,
                InputSource.FromKey(Key.D),
                KeyModifiers.None,
                ("positive", new Vector3D<double>(0.5, 0, 0)),
                ("negative", new Vector3D<double>(-0.5, 0, 0))),
            out _));

        var events = store.CancelAll();

        var stopped = Assert.Single(events);
        Assert.Same(active, stopped.Action);
        Assert.Equal(InputActionPhase.Stopped, stopped.Phase);
        Assert.Null(stopped.Source);
        Assert.False(store.TryGetValue(context, active, out _));
        Assert.False(store.TryGetValue(context, zero, out _));
    }

    private static InputRouteCandidate Candidate(
        InputAction action,
        InputContext context,
        InputSource source,
        KeyModifiers modifiers,
        params (string Name, Vector3D<double> Scale)[] definitions)
    {
        var entries = new InputBindingEntry[definitions.Length];
        var totalScale = Vector3D<double>.Zero;
        for (var index = 0; index < definitions.Length; index++)
        {
            var definition = definitions[index];
            entries[index] = new InputBindingEntry(
                ResourceKey.Create("test", $"action/{definition.Name}"),
                ResourceKey.Parse("test:action"),
                action,
                ResourceKey.Parse("test:context"),
                context,
                source,
                modifiers,
                definition.Scale,
                0,
                index,
                true);
            totalScale = new Vector3D<double>(
                totalScale.X + definition.Scale.X,
                totalScale.Y + definition.Scale.Y,
                totalScale.Z + definition.Scale.Z);
        }

        return new InputRouteCandidate(action, entries, totalScale, 0, 0);
    }
}

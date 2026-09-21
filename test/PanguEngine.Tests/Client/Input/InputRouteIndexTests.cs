using PanguEngine.Client.Input;
using PanguEngine.Input;
using PanguEngine.Registries;
using Silk.NET.Maths;

namespace PanguEngine.Tests.Client.Input;

public sealed class InputRouteIndexTests
{
    [Fact]
    public void NoneAndUndeclaredModifiersUseWildcardFallback()
    {
        var context = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Axis1D,
        [
            InputBinding.Axis1D("wildcard", context, Key.E, 1),
            InputBinding.Axis1D("shift", context, Key.E, 0.25, KeyModifiers.Shift)
        ]);
        var map = InputBindingMapTests.CreateMap(("test:action", action));
        var index = InputRouteIndex.Create(map.Entries);

        Assert.Equal(1, Assert.Single(index.GetCandidates(
            InputSource.FromKey(Key.E), context, KeyModifiers.None)).Scale.X);
        Assert.Equal(1, Assert.Single(index.GetCandidates(
            InputSource.FromKey(Key.E), context, KeyModifiers.Control)).Scale.X);
    }

    [Fact]
    public void ExactModifierOverridesWildcardOnlyForSameAction()
    {
        var context = new InputContext(InputScope.Game);
        var first = new InputAction(InputValueType.Axis1D,
        [
            InputBinding.Axis1D("wildcard", context, Key.E, 1),
            InputBinding.Axis1D("shift", context, Key.E, 0.25, KeyModifiers.Shift)
        ]);
        var second = new InputAction(InputValueType.Button,
            [InputBinding.Button("wildcard", context, Key.E)]);
        var map = InputBindingMapTests.CreateMap(("test:first", first), ("test:second", second));
        var candidates = InputRouteIndex.Create(map.Entries).GetCandidates(
            InputSource.FromKey(Key.E), context, KeyModifiers.Shift);

        Assert.Equal(2, candidates.Length);
        Assert.Equal(0.25, candidates.Single(candidate => ReferenceEquals(candidate.Action, first)).Scale.X);
        Assert.Contains(candidates, candidate => ReferenceEquals(candidate.Action, second));
    }

    [Fact]
    public void CandidateOrderUsesSelectedBindingPriorityAndOrder()
    {
        var context = new InputContext(InputScope.Game);
        var first = new InputAction(InputValueType.Button,
        [
            InputBinding.Button("wildcard", context, Key.E, priority: 100),
            InputBinding.Button("shift", context, Key.E, KeyModifiers.Shift, priority: 1)
        ]);
        var second = new InputAction(InputValueType.Button,
            [InputBinding.Button("wildcard", context, Key.E, priority: 20)]);
        var map = InputBindingMapTests.CreateMap(("test:first", first), ("test:second", second));
        var candidates = InputRouteIndex.Create(map.Entries).GetCandidates(
            InputSource.FromKey(Key.E), context, KeyModifiers.Shift);

        Assert.Equal(
            [second, first],
            candidates.Select(candidate => candidate.Action));
    }

    [Fact]
    public void DisabledEntriesAreExcludedFromCandidates()
    {
        var context = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Button,
        [
            InputBinding.Button("enabled", context, Key.E),
            InputBinding.Button("disabled", context, Key.E)
        ]);
        var map = InputBindingMapTests.CreateMap(("test:action", action));
        map.Disable(ResourceKey.Parse("test:action/disabled"));

        var candidates = InputRouteIndex.Create(map.Entries).GetCandidates(
            InputSource.FromKey(Key.E), context, KeyModifiers.None);

        var candidate = Assert.Single(candidates);
        Assert.Equal(ResourceKey.Parse("test:action/enabled"), Assert.Single(candidate.Bindings).Key);
    }

    [Fact]
    public void MissingSourceAndContextReturnEmptyCandidates()
    {
        var context = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Button,
            [InputBinding.Button("default", context, Key.E)]);
        var index = InputRouteIndex.Create(
            InputBindingMapTests.CreateMap(("test:action", action)).Entries);

        Assert.Empty(index.GetCandidates(InputSource.FromKey(Key.F), context, KeyModifiers.None));
        Assert.Empty(index.GetCandidates(
            InputSource.FromKey(Key.E), new InputContext(InputScope.Game), KeyModifiers.None));
    }
}

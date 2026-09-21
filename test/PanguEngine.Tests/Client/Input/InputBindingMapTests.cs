using PanguEngine.Client.Input;
using PanguEngine.Input;
using PanguEngine.Registries;
using Silk.NET.Maths;

namespace PanguEngine.Tests.Client.Input;

public sealed class InputBindingMapTests
{
    [Fact]
    public void CreateCopiesBindingsInActionAndDeclarationOrder()
    {
        var context = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Axis2D,
        [
            InputBinding.Axis2D("forward", context, Key.W, new Vector2D<double>(0, 1)),
            InputBinding.Axis2D("right", context, Key.D, new Vector2D<double>(1, 0))
        ]);

        var map = CreateMap(("test:move", action));

        Assert.Collection(
            map.Entries,
            entry =>
            {
                Assert.Equal(ResourceKey.Parse("test:move/forward"), entry.Key);
                Assert.Equal(0, entry.Order);
                Assert.Same(action, entry.Action);
            },
            entry =>
            {
                Assert.Equal(ResourceKey.Parse("test:move/right"), entry.Key);
                Assert.Equal(1, entry.Order);
            });
    }

    [Theory]
    [InlineData(InputValueType.Button)]
    [InlineData(InputValueType.Axis1D)]
    [InlineData(InputValueType.Axis2D)]
    [InlineData(InputValueType.Axis3D)]
    public void DigitalSourcesAcceptStateActions(InputValueType valueType)
    {
        var context = new InputContext(InputScope.Game);
        var binding = valueType switch
        {
            InputValueType.Button => InputBinding.Button("input", context, MouseButton.Left),
            InputValueType.Axis1D => InputBinding.Axis1D("input", context, MouseButton.Left, 1),
            InputValueType.Axis2D => InputBinding.Axis2D(
                "input", context, MouseButton.Left, new Vector2D<double>(1, 1)),
            InputValueType.Axis3D => InputBinding.Axis3D(
                "input", context, MouseButton.Left, new Vector3D<double>(1, 2, 3)),
            _ => throw new ArgumentOutOfRangeException(nameof(valueType))
        };
        var action = new InputAction(valueType, [binding]);

        var map = CreateMap(("test:action", action));

        var entry = Assert.Single(map.Entries);
        if (valueType == InputValueType.Axis3D)
            Assert.Equal(new Vector3D<double>(1, 2, 3), entry.Scale);
    }

    [Theory]
    [InlineData(InputValueType.Axis1D)]
    [InlineData(InputValueType.Axis2D)]
    public void TransientSourcesAcceptAxisActions(InputValueType valueType)
    {
        var context = new InputContext(InputScope.Game);
        var binding = valueType switch
        {
            InputValueType.Axis1D => InputBinding.Axis1D(
                "input", context, InputSource.MouseMove, new Vector2D<double>(1, 1)),
            InputValueType.Axis2D => InputBinding.Axis2D(
                "input", context, InputSource.MouseMove, new Vector2D<double>(1, 1)),
            _ => throw new ArgumentOutOfRangeException(nameof(valueType))
        };
        var action = new InputAction(valueType, [binding]);

        var map = CreateMap(("test:action", action));

        Assert.Single(map.Entries);
    }

    [Theory]
    [InlineData(InputValueType.Button)]
    [InlineData(InputValueType.Axis3D)]
    public void CreateRejectsIncompatibleSourceAndAction(InputValueType valueType)
    {
        var context = new InputContext(InputScope.Ui);
        var action = new InputAction(valueType,
        [
            InputBinding.Axis2D(
                "invalid", context, InputSource.MouseWheel, new Vector2D<double>(1, 1))
        ]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateMap(("test:action", action)));

        Assert.Contains("test:action/invalid", exception.Message);
        Assert.Contains(valueType.ToString(), exception.Message);
        Assert.Contains(nameof(InputSourceType.MouseWheel), exception.Message);
    }

    [Fact]
    public void CreateRejectsDuplicateSlotNamesWithinAction()
    {
        var context = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Button,
        [
            InputBinding.Button("default", context, Key.E),
            InputBinding.Button("default", context, Key.F)
        ]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateMap(("test:action", action)));

        Assert.Contains("test:action/default", exception.Message);
    }

    [Fact]
    public void RebindDisableAndResetPreserveStableDefinition()
    {
        var context = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Axis1D,
        [
            InputBinding.Axis1D("move", context, Key.W, 1)
        ]);
        var map = CreateMap(("test:action", action));
        var changes = new List<IReadOnlyList<InputBindingChange>>();
        map.Changed += changes.Add;

        var key = ResourceKey.Parse("test:action/move");
        map.SetBinding(key, InputSource.FromKey(Key.Up), KeyModifiers.Shift);
        map.Disable(key);
        map.Reset(key);

        var entry = Assert.Single(map.Entries);
        Assert.Same(action, entry.Action);
        Assert.Same(context, entry.Context);
        Assert.Equal(new Vector3D<double>(1, 0, 0), entry.Scale);
        Assert.Equal(InputSource.FromKey(Key.W), entry.Source);
        Assert.Equal(KeyModifiers.None, entry.Modifiers);
        Assert.True(entry.IsEnabled);
        Assert.Equal(3, changes.Count);
        Assert.All(changes, change => Assert.Single(change));
        Assert.Equal(key, changes[0][0].Before.Key);
        Assert.Equal(InputSource.FromKey(Key.W), changes[0][0].Before.Source);
        Assert.Equal(InputSource.FromKey(Key.Up), changes[0][0].After.Source);
        Assert.Equal(KeyModifiers.None, changes[0][0].Before.Modifiers);
        Assert.Equal(KeyModifiers.Shift, changes[0][0].After.Modifiers);
    }

    [Fact]
    public void EntriesAreCachedUntilBindingChanges()
    {
        var context = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Axis1D,
            [InputBinding.Axis1D("move", context, Key.W, 1)]);
        var map = CreateMap(("test:action", action));
        var beforeEntries = map.Entries;

        Assert.Same(beforeEntries, map.Entries);

        map.SetBinding(
            ResourceKey.Parse("test:action/move"),
            InputSource.FromKey(Key.Up),
            KeyModifiers.Shift);

        Assert.NotSame(beforeEntries, map.Entries);
        Assert.Same(map.Entries, map.Entries);
    }

    [Fact]
    public void FindConflictsUsesWildcardOverlapSemantics()
    {
        var context = new InputContext(InputScope.Game);
        var action = new InputAction(InputValueType.Button,
        [
            InputBinding.Button("wildcard", context, Key.E),
            InputBinding.Button("shift", context, Key.E, KeyModifiers.Shift),
            InputBinding.Button("control", context, Key.E, KeyModifiers.Control)
        ]);
        var map = CreateMap(("test:action", action));

        var wildcardConflicts = map.FindConflicts(InputSource.FromKey(Key.E), KeyModifiers.None);
        var controlConflicts = map.FindConflicts(InputSource.FromKey(Key.E), KeyModifiers.Control);

        Assert.Equal(
            [
                ResourceKey.Parse("test:action/wildcard"),
                ResourceKey.Parse("test:action/shift"),
                ResourceKey.Parse("test:action/control")
            ],
            wildcardConflicts.Select(entry => entry.Key));
        Assert.Equal(
            [
                ResourceKey.Parse("test:action/wildcard"),
                ResourceKey.Parse("test:action/control")
            ],
            controlConflicts.Select(entry => entry.Key));
    }

    internal static InputBindingMap CreateMap(params (string Key, InputAction Action)[] definitions)
    {
        var actions = new Registry<InputAction>(RegistryKeys.InputAction);
        var contexts = new Registry<InputContext>(RegistryKeys.InputContext);
        foreach (var definition in definitions)
            actions.Register(ResourceKey.Parse(definition.Key), definition.Action);
        var contextValues = definitions
            .SelectMany(definition => definition.Action.Bindings)
            .Select(binding => binding.Context)
            .Distinct()
            .ToArray();
        for (var index = 0; index < contextValues.Length; index++)
            contexts.Register(ResourceKey.Parse($"test:context_{index}"), contextValues[index]);
        actions.Freeze();
        contexts.Freeze();
        return InputBindingMap.CreateFromRegistries(actions, contexts);
    }
}

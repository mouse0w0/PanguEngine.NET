using System.Reflection;
using PanguEngine.Registries;
using PanguEngine.World.Entities;
using PanguEngine.World.Entities.Components;
using Silk.NET.Maths;

namespace PanguEngine.Tests.World.Entities;

public sealed class EntityManagerTests
{
    [Fact]
    public void SpawnCreatesFacadeWithRequiredValues()
    {
        using var context = EntityTestContext.Create();

        var player = context.SpawnPlayer(new Vector3D<double>(1, 2, 3), -45, 12);

        Assert.True(player.IsAlive);
        Assert.Same(BuiltinEntityTypes.Player, player.Type);
        Assert.Equal(new Vector3D<double>(1, 2, 3), player.Position.Current);
        Assert.Equal(new RotationComponent(-45, 12), player.Rotation);
        Assert.Equal(default, player.MoveIntent);
    }

    [Fact]
    public void SpawnRejectsMissingRequiredValueWithoutCreatingEntity()
    {
        using var context = EntityTestContext.Create();
        var values = new EntitySpawnBuilder()
            .Add(BuiltinEntityComponents.Position, default(PositionComponent))
            .Add(BuiltinEntityComponents.Rotation, default(RotationComponent));

        Assert.Throws<InvalidOperationException>(() =>
            context.Entities.Spawn(BuiltinEntityTypes.Player, values));
        Assert.Equal(0, context.Entities.Count);
    }

    [Fact]
    public void SpawnRejectsExtraRequiredValueWithoutCreatingEntity()
    {
        using var context = EntityTestContext.Create();
        var values = EntityTestContext.CreatePlayerValues(default)
            .Add(new ComponentType<ExtraComponent>(), new ExtraComponent(1));

        Assert.Throws<InvalidOperationException>(() =>
            context.Entities.Spawn(BuiltinEntityTypes.Player, values));
        Assert.Equal(0, context.Entities.Count);
    }

    [Fact]
    public void SpawnBuilderRejectsDuplicateAndReuse()
    {
        using var context = EntityTestContext.Create();
        var values = EntityTestContext.CreatePlayerValues(default);

        Assert.Throws<InvalidOperationException>(() =>
            values.Add(BuiltinEntityComponents.Position, default(PositionComponent)));

        context.Entities.Spawn(BuiltinEntityTypes.Player, values);

        Assert.Throws<InvalidOperationException>(() =>
            context.Entities.Spawn(BuiltinEntityTypes.Player, values));
    }

    [Fact]
    public void DestroyingMiddleEntityUpdatesMovedEntityLocation()
    {
        using var context = EntityTestContext.Create();
        var first = context.SpawnPlayer(new Vector3D<double>(1, 0, 0));
        var middle = context.SpawnPlayer(new Vector3D<double>(2, 0, 0));
        var last = context.SpawnPlayer(new Vector3D<double>(3, 0, 0));

        middle.Destroy();

        Assert.Equal(new Vector3D<double>(1, 0, 0), first.Position.Current);
        Assert.Equal(new Vector3D<double>(3, 0, 0), last.Position.Current);
    }

    [Fact]
    public void ReusedSlotDoesNotReviveOldHandle()
    {
        using var context = EntityTestContext.Create();
        var removed = context.SpawnPlayer(default);
        var oldHandle = removed.Handle;
        removed.Destroy();

        var replacement = context.SpawnPlayer(default);

        Assert.False(context.Entities.IsAlive(oldHandle));
        Assert.NotEqual(oldHandle, replacement.Handle);
        Assert.Throws<InvalidOperationException>(() => _ = removed.Position);
    }

    [Fact]
    public void DestroyRejectsRepeatedCallForSameEntity()
    {
        using var context = EntityTestContext.Create();
        var player = context.SpawnPlayer(default);
        player.Destroy();

        Assert.Throws<InvalidOperationException>(() => player.Destroy());
    }

    [Fact]
    public void FactoryFailureInvalidatesLeakedHandleAndRollsBackStorage()
    {
        var (componentTypes, entityTypes) = EntityTestContext.CreateMutableRegistries();
        EntityHandle leakedHandle = default;
        var failingType = new EntityType<FailingEntity>(
            BuiltinEntityTypes.Player.RequiredSignature,
            (entities, handle, type) =>
            {
                leakedHandle = handle;
                throw new TestException();
            });
        entityTypes.Register(ResourceKey.Parse("test:failing"), failingType);
        componentTypes.Freeze();
        entityTypes.Freeze();
        var entities = new EntityManager(entityTypes);

        Assert.Throws<TestException>(() =>
            entities.Spawn(failingType, EntityTestContext.CreatePlayerValues(default)));
        Assert.True(entities.TryGetArchetype(failingType, out var failedArchetype));
        Assert.Equal(0, failedArchetype.Count);
        Assert.Equal(0, entities.Count);
        var replacement = entities.Spawn(
            BuiltinEntityTypes.Player,
            EntityTestContext.CreatePlayerValues(default));

        Assert.Equal(1, entities.Count);
        Assert.Equal(1u, replacement.NetworkId);
        Assert.False(entities.IsAlive(leakedHandle));
        Assert.NotEqual(leakedHandle, replacement.Handle);
        entities.Destroy();
    }

    [Fact]
    public void DifferentEntityTypesKeepSeparateArchetypesAndShareNetworkIdAllocation()
    {
        var (componentTypes, entityTypes) = EntityTestContext.CreateMutableRegistries();
        var alternateType = new EntityType<AlternateEntity>(
            BuiltinEntityTypes.Player.RequiredSignature,
            static (entities, handle, type) => new AlternateEntity(entities, handle, type));
        entityTypes.Register(ResourceKey.Parse("test:alternate"), alternateType);
        componentTypes.Freeze();
        entityTypes.Freeze();
        var entities = new EntityManager(entityTypes);

        var first = entities.Spawn(BuiltinEntityTypes.Player, EntityTestContext.CreatePlayerValues(default));
        var alternate = entities.Spawn(alternateType, EntityTestContext.CreatePlayerValues(default));

        Assert.Equal(0u, first.NetworkId);
        Assert.Equal(1u, alternate.NetworkId);
        first.Destroy();
        var replacement = entities.Spawn(BuiltinEntityTypes.Player, EntityTestContext.CreatePlayerValues(default));
        Assert.Equal(first.Handle.Index, replacement.Handle.Index);
        Assert.Equal(2u, replacement.NetworkId);
        Assert.Equal(0u, first.NetworkId);
        Assert.Equal(1u, alternate.NetworkId);

        Assert.True(entities.TryGetArchetype(BuiltinEntityTypes.Player, out var playerArchetype));
        Assert.True(entities.TryGetArchetype(alternateType, out var alternateArchetype));
        Assert.NotSame(playerArchetype, alternateArchetype);
        entities.Destroy();
    }

    [Fact]
    public void NetworkIdsWrapFromMaxValueToZero()
    {
        var entities = new EntityManager();
        typeof(EntityManager).GetField("_nextNetworkId", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(entities, uint.MaxValue);

        var first = entities.Spawn(BuiltinEntityTypes.Player, EntityTestContext.CreatePlayerValues(default));
        var second = entities.Spawn(BuiltinEntityTypes.Player, EntityTestContext.CreatePlayerValues(default));

        Assert.Equal(uint.MaxValue, first.NetworkId);
        Assert.Equal(0u, second.NetworkId);
        entities.Destroy();
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(uint.MaxValue)]
    public void ExplicitNetworkIdDoesNotAdvanceAutomaticAllocation(uint networkId)
    {
        using var context = EntityTestContext.Create();
        var first = context.SpawnPlayer(default);
        var replica = context.Entities.Spawn(
            BuiltinEntityTypes.Player, EntityTestContext.CreatePlayerValues(default), networkId);
        var next = context.SpawnPlayer(default);

        Assert.Equal(0u, first.NetworkId);
        Assert.Equal(networkId, replica.NetworkId);
        Assert.Equal(1u, next.NetworkId);
    }

    [Fact]
    public void UpdateScopeRejectsNestedUpdateAndStructuralChanges()
    {
        using var context = EntityTestContext.Create();
        context.Entities.BeginUpdate();

        try
        {
            Assert.Throws<InvalidOperationException>(() => context.Entities.BeginUpdate());
            Assert.Throws<InvalidOperationException>(() => context.SpawnPlayer(default));
        }
        finally
        {
            context.Entities.EndUpdate();
        }

        var player = context.SpawnPlayer(default);
        Assert.True(player.IsAlive);
    }

    [Fact]
    public void AccessFromAnotherThreadIsRejected()
    {
        using var context = EntityTestContext.Create();
        var player = context.SpawnPlayer(default);

        Exception? exception = null;
        var thread = new Thread(() => exception = Record.Exception(() => _ = player.IsAlive));
        thread.Start();
        thread.Join();

        Assert.IsType<InvalidOperationException>(exception);
    }

    private readonly record struct ExtraComponent(int Value);

    private sealed class FailingEntity(EntityManager entities, EntityHandle handle, EntityType type)
        : Entity(entities, handle, type)
    {
    }

    private sealed class AlternateEntity(EntityManager entities, EntityHandle handle, EntityType type)
        : Entity(entities, handle, type)
    {
    }

    private sealed class TestException : Exception
    {
    }
}

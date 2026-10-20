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
            (entities, handle) =>
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
        Assert.False(entities.IsAlive(leakedHandle));
        Assert.NotEqual(leakedHandle, replacement.Handle);
        entities.Destroy();
    }

    [Fact]
    public void DifferentEntityTypesWithSameRequiredSignatureUseDifferentArchetypes()
    {
        var (componentTypes, entityTypes) = EntityTestContext.CreateMutableRegistries();
        var alternateType = new EntityType<AlternateEntity>(
            BuiltinEntityTypes.Player.RequiredSignature,
            static (entities, handle) => new AlternateEntity(entities, handle));
        entityTypes.Register(ResourceKey.Parse("test:alternate"), alternateType);
        componentTypes.Freeze();
        entityTypes.Freeze();
        var entities = new EntityManager(entityTypes);

        entities.Spawn(BuiltinEntityTypes.Player, EntityTestContext.CreatePlayerValues(default));
        entities.Spawn(alternateType, EntityTestContext.CreatePlayerValues(default));

        Assert.True(entities.TryGetArchetype(BuiltinEntityTypes.Player, out var playerArchetype));
        Assert.True(entities.TryGetArchetype(alternateType, out var alternateArchetype));
        Assert.NotSame(playerArchetype, alternateArchetype);
        entities.Destroy();
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
    public async Task AccessFromAnotherThreadIsRejected()
    {
        using var context = EntityTestContext.Create();
        var player = context.SpawnPlayer(default);

        var exception = await Record.ExceptionAsync(() => Task.Run(() => player.IsAlive));

        Assert.IsType<InvalidOperationException>(exception);
    }

    private readonly record struct ExtraComponent(int Value);

    private sealed class FailingEntity(EntityManager entities, EntityHandle handle) : Entity(entities, handle)
    {
    }

    private sealed class AlternateEntity(EntityManager entities, EntityHandle handle) : Entity(entities, handle)
    {
    }

    private sealed class TestException : Exception
    {
    }
}

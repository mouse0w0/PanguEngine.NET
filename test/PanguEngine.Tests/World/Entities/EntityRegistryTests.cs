using PanguEngine.Registries;
using PanguEngine.World.Entities;

namespace PanguEngine.Tests.World.Entities;

public sealed class EntityRegistryTests
{
    [Fact]
    public void BuiltinEntityRegistriesAreManagedFrozenAndResolveHolders()
    {
        var manager = TestRegistryBootstrap.Manager;

        Assert.Same(
            BuiltinRegistries.EntityComponentType,
            manager.Get<ComponentType>(RegistryKeys.EntityComponentType));
        Assert.Same(
            BuiltinRegistries.EntityType,
            manager.Get<EntityType>(RegistryKeys.EntityType));
        Assert.True(BuiltinRegistries.EntityComponentType.IsFrozen);
        Assert.True(BuiltinRegistries.EntityType.IsFrozen);

        var componentHolder = manager.CreateHolder<ComponentType>(
            RegistryKeys.EntityComponentType,
            ResourceKey.Parse("pangu:position"));
        var entityTypeHolder = manager.CreateHolder<EntityType>(
            RegistryKeys.EntityType,
            ResourceKey.Parse("pangu:player"));

        Assert.Same(BuiltinEntityComponents.Position, componentHolder.Value);
        Assert.Same(BuiltinEntityTypes.Player, entityTypeHolder.Value);
    }

    [Fact]
    public void ComponentTypeCanBeRegisteredAfterBaseRegistrationFailure()
    {
        var registry = new Registry<ComponentType>(
            ResourceKey.Parse("test:component_type"), (current, _, value) => value.ValidateRegistration(current));
        registry.Register(ResourceKey.Parse("test:first"), new ComponentType<FirstComponent>());
        var second = new ComponentType<SecondComponent>();

        Assert.Throws<InvalidOperationException>(() =>
            registry.Register(ResourceKey.Parse("test:first"), second));

        registry.Register(ResourceKey.Parse("test:second"), second);
        Assert.Same(second, registry.Get(ResourceKey.Parse("test:second")));
    }

    [Fact]
    public void RegistryManagerWritableAccessUsesComponentTypeValidation()
    {
        var manager = new RegistryManager();
        var registry = new Registry<ComponentType>(
            ResourceKey.Parse("test:component_type"), (current, _, value) => value.ValidateRegistration(current));
        manager.Register(registry);
        var writable = manager.GetWritable<ComponentType>(registry.Key);
        writable.Register(ResourceKey.Parse("test:first"), new ComponentType<TestComponent>());

        Assert.Throws<InvalidOperationException>(() =>
            writable.Register(ResourceKey.Parse("test:second"), new ComponentType<TestComponent>()));
    }

    [Fact]
    public void RegistryManagerWritableAccessUsesEntityTypeValidation()
    {
        var (_, entityTypes, first, _) = CreateRegistries();
        var manager = new RegistryManager();
        manager.Register(entityTypes);
        var writable = manager.GetWritable<EntityType>(entityTypes.Key);
        var type = new EntityType<TestEntity>(
            [first, first],
            static (entities, handle) => new TestEntity(entities, handle));

        Assert.Throws<InvalidOperationException>(() =>
            writable.Register(ResourceKey.Parse("test:duplicate"), type));
    }

    [Fact]
    public void EntityTypeValidationRejectsUnregisteredComponents()
    {
        var (_, entityTypes, _, _) = CreateRegistries();
        var type = new EntityType<TestEntity>(
            [new ComponentType<UnregisteredComponent>()],
            static (entities, handle) => new TestEntity(entities, handle));

        Assert.Throws<InvalidOperationException>(() =>
            entityTypes.Register(ResourceKey.Parse("test:unregistered"), type));
    }

    [Fact]
    public void EntityTypeValidationAcceptsComponentsInAnyRegistryIdOrder()
    {
        var (_, entityTypes, first, second) = CreateRegistries();
        var type = new EntityType<TestEntity>(
            [second, first],
            static (entities, handle) => new TestEntity(entities, handle));
        var key = ResourceKey.Parse("test:unordered");

        entityTypes.Register(key, type);

        Assert.Same(type, entityTypes.Get(key));
    }

    [Fact]
    public void FrozenComponentTypeRegistryRejectsFurtherRegistration()
    {
        var registry = new Registry<ComponentType>(
            ResourceKey.Parse("test:component_type"), (current, _, value) => value.ValidateRegistration(current));
        registry.Freeze();

        Assert.Throws<InvalidOperationException>(() =>
            registry.Register(ResourceKey.Parse("test:late"), new ComponentType<TestComponent>()));
    }

    private static (
        Registry<ComponentType> ComponentTypes,
        Registry<EntityType> EntityTypes,
        ComponentType<FirstComponent> First,
        ComponentType<SecondComponent> Second) CreateRegistries()
    {
        var componentTypes = new Registry<ComponentType>(
            ResourceKey.Parse("test:component_type"), (current, _, value) => value.ValidateRegistration(current));
        var first = new ComponentType<FirstComponent>();
        var second = new ComponentType<SecondComponent>();
        componentTypes.Register(ResourceKey.Parse("test:first"), first);
        componentTypes.Register(ResourceKey.Parse("test:second"), second);
        return (
            componentTypes,
            new Registry<EntityType>(ResourceKey.Parse("test:entity_type"),
                (_, _, value) => value.ValidateRegistration(componentTypes)),
            first,
            second);
    }

    private readonly record struct TestComponent(int Value);

    private readonly record struct FirstComponent(int Value);

    private readonly record struct SecondComponent(int Value);

    private readonly record struct UnregisteredComponent(int Value);

    private sealed class TestEntity(EntityManager entities, EntityHandle handle) : Entity(entities, handle)
    {
    }
}

using PanguEngine.Registries;

namespace PanguEngine.Tests.Registries;

public sealed class RegistryManagerTests
{
    [Fact]
    public void FreezeAllBindsCreatedHolder()
    {
        var manager = new RegistryManager();
        var registryKey = ResourceKey.Parse("pangu:test");
        var entryKey = ResourceKey.Parse("pangu:stone");
        var value = new object();
        var registry = new Registry<object>(registryKey);
        registry.Register(entryKey, value);
        manager.Register(registry);

        var holder = manager.CreateHolder<object>(registryKey, entryKey);

        Assert.Equal(new ResourceAddress(registryKey, entryKey), holder.Address);
        Assert.False(holder.IsBound);
        Assert.False(holder.TryGet(out var missing));
        Assert.Null(missing);
        Assert.Throws<InvalidOperationException>(() => holder.Value);

        manager.FreezeAll();

        Assert.True(holder.IsBound);
        Assert.True(holder.TryGet(out var actual));
        Assert.Same(value, actual);
        Assert.Same(value, holder.Value);
        Assert.Equal(new ResourceAddress(registryKey, entryKey), holder.Address);
    }

    [Fact]
    public void FreezeAllDoesNotUseDefaultedRegistryFallback()
    {
        var manager = new RegistryManager();
        var registryKey = ResourceKey.Parse("pangu:test");
        var defaultKey = ResourceKey.Parse("pangu:air");
        var missingKey = ResourceKey.Parse("pangu:missing");
        var registry = new DefaultedRegistry<object>(registryKey, defaultKey);
        registry.Register(defaultKey, new object());
        manager.Register(registry);
        var holder = manager.CreateHolder<object>(registryKey, missingKey);

        Assert.Throws<KeyNotFoundException>(() => manager.FreezeAll());
        Assert.False(holder.IsBound);
    }

    [Fact]
    public void FreezeAllIsReentrantAfterHolderResolution()
    {
        var manager = new RegistryManager();
        var registryKey = ResourceKey.Parse("pangu:test");
        var entryKey = ResourceKey.Parse("pangu:stone");
        var value = new object();
        var registry = new Registry<object>(registryKey);
        registry.Register(entryKey, value);
        manager.Register(registry);
        var holder = manager.CreateHolder<object>(registryKey, entryKey);

        manager.FreezeAll();
        manager.FreezeAll();

        Assert.True(holder.IsBound);
        Assert.Same(value, holder.Value);
    }

    [Fact]
    public void CreateHolderAfterFreezeResolvesImmediately()
    {
        var manager = new RegistryManager();
        var registryKey = ResourceKey.Parse("pangu:test");
        var entryKey = ResourceKey.Parse("pangu:stone");
        var value = new object();
        var registry = new Registry<object>(registryKey);
        registry.Register(entryKey, value);
        manager.Register(registry);
        manager.FreezeAll();

        var holder = manager.CreateHolder<object>(registryKey, entryKey);

        Assert.True(holder.IsBound);
        Assert.Same(value, holder.Value);
    }

    [Fact]
    public void FailedCreateHolderAfterFreezeDoesNotAffectLaterFreezeAll()
    {
        var manager = new RegistryManager();
        var registryKey = ResourceKey.Parse("pangu:test");
        var registry = new Registry<object>(registryKey);
        manager.Register(registry);
        manager.FreezeAll();

        Assert.Throws<KeyNotFoundException>(() =>
            manager.CreateHolder<object>(registryKey, ResourceKey.Parse("pangu:missing")));

        manager.FreezeAll();
    }
}
using PanguEngine.Registries;

namespace PanguEngine.Tests.Registries;

public sealed class DefaultedRegistryTests
{
    [Fact]
    public void ConstructorStoresDefaultKey()
    {
        var defaultKey = ResourceKey.Parse("pangu:air");
        var registry = CreateRegistry(defaultKey);

        Assert.Equal(defaultKey, registry.DefaultKey);
    }

    [Fact]
    public void ConstructorRejectsInvalidDefaultKey()
    {
        Assert.Throws<ArgumentException>(() =>
            new DefaultedRegistry<object>(ResourceKey.Parse("pangu:test"), default!));
    }

    [Fact]
    public void DefaultAccessorsThrowBeforeFreeze()
    {
        var defaultKey = ResourceKey.Parse("pangu:air");
        var registry = CreateRegistry(defaultKey);
        registry.Register(defaultKey, new object());

        Assert.Throws<InvalidOperationException>(() => registry.DefaultEntry);
        Assert.Throws<InvalidOperationException>(() => registry.DefaultValue);
        Assert.Throws<InvalidOperationException>(() => registry.DefaultId);
    }

    [Fact]
    public void FreezeCachesDefaultEntryValueAndId()
    {
        var defaultKey = ResourceKey.Parse("pangu:air");
        var registry = CreateRegistry(defaultKey);
        var value = new object();
        var entry = registry.Register(defaultKey, value);

        registry.Freeze();

        Assert.True(registry.IsFrozen);
        Assert.Same(entry, registry.DefaultEntry);
        Assert.Same(value, registry.DefaultValue);
        Assert.Equal(entry.Id, registry.DefaultId);
    }

    [Fact]
    public void FreezeRejectsUnregisteredDefaultKey()
    {
        var registry = CreateRegistry(ResourceKey.Parse("pangu:missing"));

        Assert.Throws<KeyNotFoundException>(() => registry.Freeze());
        Assert.False(registry.IsFrozen);
        Assert.Throws<InvalidOperationException>(() => registry.DefaultEntry);
        Assert.Throws<InvalidOperationException>(() => registry.DefaultValue);
        Assert.Throws<InvalidOperationException>(() => registry.DefaultId);
    }

    [Fact]
    public void GetFallsBackToDefaultValueAfterFreeze()
    {
        var defaultKey = ResourceKey.Parse("pangu:air");
        var registry = CreateRegistry(defaultKey);
        var defaultValue = new object();
        registry.Register(defaultKey, defaultValue);
        registry.Freeze();

        Assert.Same(defaultValue, registry.Get(ResourceKey.Parse("pangu:missing")));
        Assert.Same(defaultValue, registry.Get(99));
    }

    [Fact]
    public void GetDoesNotFallBackBeforeFreeze()
    {
        var defaultKey = ResourceKey.Parse("pangu:air");
        var registry = CreateRegistry(defaultKey);
        registry.Register(defaultKey, new object());

        Assert.Throws<KeyNotFoundException>(() => registry.Get(ResourceKey.Parse("pangu:missing")));
        Assert.Throws<KeyNotFoundException>(() => registry.Get(99));
        Assert.Throws<KeyNotFoundException>(() => registry.GetEntry(ResourceKey.Parse("pangu:missing")));
        Assert.Throws<KeyNotFoundException>(() => registry.GetEntry(99));
    }

    [Fact]
    public void GetEntryFallsBackToDefaultEntryAfterFreeze()
    {
        var defaultKey = ResourceKey.Parse("pangu:air");
        var registry = CreateRegistry(defaultKey);
        var defaultEntry = registry.Register(defaultKey, new object());
        registry.Freeze();

        Assert.Same(defaultEntry, registry.GetEntry(ResourceKey.Parse("pangu:missing")));
        Assert.Same(defaultEntry, registry.GetEntry(99));
    }

    [Fact]
    public void GetAndGetEntryReturnRegisteredNonDefaultEntryAfterFreeze()
    {
        var defaultKey = ResourceKey.Parse("pangu:air");
        var otherKey = ResourceKey.Parse("pangu:stone");
        var registry = CreateRegistry(defaultKey);
        var defaultEntry = registry.Register(defaultKey, new object());
        var otherEntry = registry.Register(otherKey, new object());
        registry.Freeze();

        Assert.Same(otherEntry.Value, registry.Get(otherKey));
        Assert.Same(otherEntry.Value, registry.Get(otherEntry.Id));
        Assert.Same(otherEntry, registry.GetEntry(otherKey));
        Assert.Same(otherEntry, registry.GetEntry(otherEntry.Id));
        Assert.Same(defaultEntry.Value, registry.Get(ResourceKey.Parse("pangu:missing")));
        Assert.Same(defaultEntry, registry.GetEntry(ResourceKey.Parse("pangu:missing")));
        Assert.Same(defaultEntry, registry.GetEntry(99));
    }

    [Fact]
    public void TryGetAndTryGetEntryDoNotFallBack()
    {
        var defaultKey = ResourceKey.Parse("pangu:air");
        var registry = CreateRegistry(defaultKey);
        registry.Register(defaultKey, new object());
        registry.Freeze();

        Assert.False(registry.TryGet(ResourceKey.Parse("pangu:missing"), out var value));
        Assert.Null(value);
        Assert.False(registry.TryGet(99, out value));
        Assert.Null(value);
        Assert.False(registry.TryGetEntry(ResourceKey.Parse("pangu:missing"), out var entry));
        Assert.Null(entry);
        Assert.False(registry.TryGetEntry(99, out entry));
        Assert.Null(entry);
    }

    [Fact]
    public void ContainsDoesNotFallBack()
    {
        var defaultKey = ResourceKey.Parse("pangu:air");
        var registry = CreateRegistry(defaultKey);
        registry.Register(defaultKey, new object());
        registry.Freeze();

        Assert.False(registry.ContainsKey(ResourceKey.Parse("pangu:missing")));
        Assert.False(registry.ContainsId(99));
    }

    [Fact]
    public void ReverseLookupDoesNotFallBackToDefaultValue()
    {
        var defaultKey = ResourceKey.Parse("pangu:air");
        var registry = CreateRegistry(defaultKey);
        var defaultValue = new object();
        var missingValue = new object();
        var defaultEntry = registry.Register(defaultKey, defaultValue);
        registry.Freeze();

        Assert.Equal(defaultKey, registry.GetKey(defaultValue));
        Assert.Equal(defaultEntry.Id, registry.GetId(defaultValue));
        Assert.False(registry.TryGetKey(missingValue, out var missingKey));
        Assert.Equal(default, missingKey);
        Assert.False(registry.TryGetId(missingValue, out var missingId));
        Assert.Equal(-1, missingId);
        Assert.Throws<KeyNotFoundException>(() => registry.GetKey(missingValue));
        Assert.Throws<KeyNotFoundException>(() => registry.GetId(missingValue));
    }

    private static DefaultedRegistry<object> CreateRegistry(ResourceKey defaultKey) =>
        new(ResourceKey.Parse("pangu:test"), defaultKey);
}
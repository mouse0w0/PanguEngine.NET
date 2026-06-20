using PanguEngine.Registries;

namespace PanguEngine.Tests.Registries;

public sealed class DefaultedRegistryTests
{
    [Fact]
    public void DefaultStateUsesUnsetValues()
    {
        var registry = CreateRegistry();

        Assert.Null(registry.DefaultKey);
        Assert.Equal(-1, registry.DefaultId);
        Assert.Null(registry.DefaultValue);
    }

    [Fact]
    public void SetDefaultStoresKeyBeforeFreeze()
    {
        var registry = CreateRegistry();
        var key = ResourceKey.Parse("pangu:air");

        registry.SetDefault(key);

        Assert.Equal(key, registry.DefaultKey);
        Assert.Equal(-1, registry.DefaultId);
        Assert.Null(registry.DefaultValue);
    }

    [Fact]
    public void SetDefaultRejectsInvalidKey()
    {
        var registry = CreateRegistry();

        Assert.Throws<ArgumentException>(() => registry.SetDefault(default));
    }

    [Fact]
    public void SetDefaultRejectsFrozenRegistry()
    {
        var registry = CreateRegistry();
        registry.Freeze();

        Assert.Throws<InvalidOperationException>(() => registry.SetDefault(ResourceKey.Parse("pangu:air")));
    }

    [Fact]
    public void SetDefaultUsesLastKeyBeforeFreeze()
    {
        var registry = CreateRegistry();
        var first = ResourceKey.Parse("pangu:first");
        var second = ResourceKey.Parse("pangu:second");
        registry.Register(first, new object());
        var expected = registry.Register(second, new object());

        registry.SetDefault(first);
        registry.SetDefault(second);
        registry.Freeze();

        Assert.Equal(second, registry.DefaultKey);
        Assert.Equal(expected.Id, registry.DefaultId);
        Assert.Same(expected.Value, registry.DefaultValue);
    }

    [Fact]
    public void FreezeCachesDefaultIdAndValue()
    {
        var registry = CreateRegistry();
        var key = ResourceKey.Parse("pangu:air");
        var value = new object();
        var entry = registry.Register(key, value);
        registry.SetDefault(key);

        registry.Freeze();

        Assert.True(registry.IsFrozen);
        Assert.Equal(entry.Id, registry.DefaultId);
        Assert.Same(value, registry.DefaultValue);
    }

    [Fact]
    public void FreezeRejectsUnregisteredDefaultKey()
    {
        var registry = CreateRegistry();
        registry.SetDefault(ResourceKey.Parse("pangu:missing"));

        Assert.Throws<KeyNotFoundException>(() => registry.Freeze());
    }

    [Fact]
    public void GetFallsBackToDefaultValueAfterFreeze()
    {
        var registry = CreateRegistry();
        var defaultValue = new object();
        registry.Register(ResourceKey.Parse("pangu:air"), defaultValue);
        registry.SetDefault(ResourceKey.Parse("pangu:air"));
        registry.Freeze();

        Assert.Same(defaultValue, registry.Get(ResourceKey.Parse("pangu:missing")));
        Assert.Same(defaultValue, registry.Get(99));
    }

    [Fact]
    public void GetDoesNotFallBackBeforeFreeze()
    {
        var registry = CreateRegistry();
        registry.Register(ResourceKey.Parse("pangu:air"), new object());
        registry.SetDefault(ResourceKey.Parse("pangu:air"));

        Assert.Throws<KeyNotFoundException>(() => registry.Get(ResourceKey.Parse("pangu:missing")));
        Assert.Throws<KeyNotFoundException>(() => registry.Get(99));
    }

    [Fact]
    public void GetEntryDoesNotFallBackToDefaultEntry()
    {
        var registry = CreateRegistry();
        registry.Register(ResourceKey.Parse("pangu:air"), new object());
        registry.SetDefault(ResourceKey.Parse("pangu:air"));
        registry.Freeze();

        Assert.Throws<KeyNotFoundException>(() => registry.GetEntry(ResourceKey.Parse("pangu:missing")));
        Assert.Throws<KeyNotFoundException>(() => registry.GetEntry(99));
    }

    [Fact]
    public void TryGetAndTryGetEntryDoNotFallBack()
    {
        var registry = CreateRegistry();
        registry.Register(ResourceKey.Parse("pangu:air"), new object());
        registry.SetDefault(ResourceKey.Parse("pangu:air"));
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
        var registry = CreateRegistry();
        registry.Register(ResourceKey.Parse("pangu:air"), new object());
        registry.SetDefault(ResourceKey.Parse("pangu:air"));
        registry.Freeze();

        Assert.False(registry.ContainsKey(ResourceKey.Parse("pangu:missing")));
        Assert.False(registry.ContainsId(99));
    }

    [Fact]
    public void ReverseLookupDoesNotFallBackToDefaultValue()
    {
        var registry = CreateRegistry();
        var defaultValue = new object();
        var missingValue = new object();
        var defaultKey = ResourceKey.Parse("pangu:air");
        var defaultEntry = registry.Register(defaultKey, defaultValue);
        registry.SetDefault(defaultKey);
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

    private static DefaultedRegistry<object> CreateRegistry() => new(ResourceKey.Parse("pangu:test"));
}
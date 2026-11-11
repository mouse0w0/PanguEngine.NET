using PanguEngine.Registries;

namespace PanguEngine.Tests.Registries;

public sealed class DefaultedRegistryTests
{
    [Fact]
    public void DefaultEntryUsesValidationBeforeItCanBeFrozen()
    {
        var defaultKey = ResourceKey.Parse("pangu:air");
        var rejected = new object();
        var registry = new DefaultedRegistry<object>(
            ResourceKey.Parse("pangu:test"), defaultKey, (_, _, candidate) =>
            {
                if (ReferenceEquals(candidate, rejected))
                    throw new InvalidOperationException("Rejected default.");
            });

        Assert.Throws<InvalidOperationException>(() => registry.Register(defaultKey, rejected));

        var accepted = new object();
        registry.Register(defaultKey, accepted);
        registry.Freeze();

        Assert.Same(accepted, registry.DefaultValue);
    }

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
    }

    [Fact]
    public void FreezeCachesDefaultEntryAndValue()
    {
        var defaultKey = ResourceKey.Parse("pangu:air");
        var registry = CreateRegistry(defaultKey);
        var value = new object();
        var entry = registry.Register(defaultKey, value);

        registry.Freeze();

        Assert.True(registry.IsFrozen);
        Assert.Same(entry, registry.DefaultEntry);
        Assert.Same(value, registry.DefaultValue);
    }

    [Fact]
    public void FreezeRejectsUnregisteredDefaultKey()
    {
        var registry = CreateRegistry(ResourceKey.Parse("pangu:missing"));

        Assert.Throws<KeyNotFoundException>(() => registry.Freeze());
        Assert.False(registry.IsFrozen);
        Assert.Throws<InvalidOperationException>(() => registry.DefaultEntry);
        Assert.Throws<InvalidOperationException>(() => registry.DefaultValue);
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
    }

    [Fact]
    public void GetDoesNotFallBackBeforeFreeze()
    {
        var defaultKey = ResourceKey.Parse("pangu:air");
        var registry = CreateRegistry(defaultKey);
        registry.Register(defaultKey, new object());

        Assert.Throws<KeyNotFoundException>(() => registry.Get(ResourceKey.Parse("pangu:missing")));
        Assert.Throws<KeyNotFoundException>(() => registry.GetEntry(ResourceKey.Parse("pangu:missing")));
    }

    [Fact]
    public void GetEntryFallsBackToDefaultEntryAfterFreeze()
    {
        var defaultKey = ResourceKey.Parse("pangu:air");
        var registry = CreateRegistry(defaultKey);
        var defaultEntry = registry.Register(defaultKey, new object());
        registry.Freeze();

        Assert.Same(defaultEntry, registry.GetEntry(ResourceKey.Parse("pangu:missing")));
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
        Assert.Same(otherEntry, registry.GetEntry(otherKey));
        Assert.Same(defaultEntry.Value, registry.Get(ResourceKey.Parse("pangu:missing")));
        Assert.Same(defaultEntry, registry.GetEntry(ResourceKey.Parse("pangu:missing")));
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
        Assert.False(registry.TryGetEntry(ResourceKey.Parse("pangu:missing"), out var entry));
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
    }

    [Fact]
    public void ReverseLookupDoesNotFallBackToDefaultValue()
    {
        var defaultKey = ResourceKey.Parse("pangu:air");
        var registry = CreateRegistry(defaultKey);
        var defaultValue = new object();
        var missingValue = new object();
        registry.Register(defaultKey, defaultValue);
        registry.Freeze();

        Assert.Equal(defaultKey, registry.GetKey(defaultValue));
        Assert.False(registry.TryGetKey(missingValue, out var missingKey));
        Assert.Equal(default, missingKey);
        Assert.Throws<KeyNotFoundException>(() => registry.GetKey(missingValue));
    }

    private static DefaultedRegistry<object> CreateRegistry(ResourceKey defaultKey) =>
        new(ResourceKey.Parse("pangu:test"), defaultKey);
}

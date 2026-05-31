using PanguEngine.Registry;

namespace PanguEngine.Tests.Registry;

public sealed class RegistryTests
{
    [Fact]
    public void RegisterAssignsIncrementingIds()
    {
        var registry = CreateRegistry();

        var first = registry.Register(ResourceKey.Parse("pangu:first"), new object());
        var second = registry.Register(ResourceKey.Parse("pangu:second"), new object());

        Assert.Equal(0, first.Id);
        Assert.Equal(1, second.Id);
    }

    [Fact]
    public void GetReturnsValuesByKeyAndId()
    {
        var registry = CreateRegistry();
        var value = new object();
        var entry = registry.Register(ResourceKey.Parse("pangu:stone"), value);

        Assert.Same(value, registry.Get(ResourceKey.Parse("pangu:stone")));
        Assert.Same(value, registry.Get(entry.Id));
    }

    [Fact]
    public void RegisterRejectsDuplicateKey()
    {
        var registry = CreateRegistry();
        var key = ResourceKey.Parse("pangu:stone");

        registry.Register(key, new object());

        Assert.Throws<InvalidOperationException>(() => registry.Register(key, new object()));
    }

    [Fact]
    public void RegisterRejectsNullValue()
    {
        var registry = new Registry<object?>(ResourceKey.Parse("pangu:test"));

        Assert.Throws<ArgumentNullException>(() => registry.Register(ResourceKey.Parse("pangu:null"), null));
    }

    [Fact]
    public void FreezePreventsFurtherRegistration()
    {
        var registry = CreateRegistry();
        registry.Freeze();

        Assert.True(registry.IsFrozen);
        Assert.Throws<InvalidOperationException>(() =>
            registry.Register(ResourceKey.Parse("pangu:stone"), new object()));
    }

    [Fact]
    public void TryGetUsesNullableContractShape()
    {
        var registry = CreateRegistry();

        Assert.False(registry.TryGet(ResourceKey.Parse("pangu:missing"), out var value));
        Assert.Null(value);
        Assert.False(registry.TryGetEntry(12, out var entry));
        Assert.Null(entry);
    }

    [Fact]
    public void EntriesAreOrderedByIdAndCannotMutateRegistry()
    {
        var registry = CreateRegistry();
        registry.Register(ResourceKey.Parse("pangu:first"), new object());
        registry.Register(ResourceKey.Parse("pangu:second"), new object());

        Assert.Equal([0, 1], registry.Entries.Select(entry => entry.Id).ToArray());
        Assert.False(registry.Entries is List<RegistryEntry<object>>);
    }

    [Fact]
    public void RegistryCanBeManagedThroughNonGenericInterface()
    {
        var registry = CreateRegistry();
        IRegistry managed = registry;

        Assert.Equal(ResourceKey.Parse("pangu:test"), managed.Key);
        Assert.Equal(typeof(object), managed.ValueType);
    }

    private static Registry<object> CreateRegistry() => new(ResourceKey.Parse("pangu:test"));
}
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
        var registry = CreateRegistry();

        Assert.Throws<ArgumentNullException>(() => registry.Register(ResourceKey.Parse("pangu:null"), null!));
    }

    [Fact]
    public void GetKeyAndGetIdReturnRegisteredIdentity()
    {
        var registry = CreateRegistry();
        var key = ResourceKey.Parse("pangu:stone");
        var value = new object();
        var entry = registry.Register(key, value);

        Assert.Equal(key, registry.GetKey(value));
        Assert.Equal(entry.Id, registry.GetId(value));
    }

    [Fact]
    public void TryGetKeyAndTryGetIdReturnRegisteredIdentity()
    {
        var registry = CreateRegistry();
        var key = ResourceKey.Parse("pangu:stone");
        var value = new object();
        var entry = registry.Register(key, value);

        Assert.True(registry.TryGetKey(value, out var actualKey));
        Assert.Equal(key, actualKey);
        Assert.True(registry.TryGetId(value, out var actualId));
        Assert.Equal(entry.Id, actualId);
    }

    [Fact]
    public void ReverseLookupUsesReferenceEquality()
    {
        var registry = new Registry<ValueEqualResource>(ResourceKey.Parse("pangu:test"));
        var registered = new ValueEqualResource(7);
        var equalButDifferent = new ValueEqualResource(7);
        var key = ResourceKey.Parse("pangu:stone");
        registry.Register(key, registered);

        Assert.Equal(key, registry.GetKey(registered));
        Assert.False(registry.TryGetKey(equalButDifferent, out var actualKey));
        Assert.Equal(default, actualKey);
    }

    [Fact]
    public void RegisterRejectsDuplicateValueInstance()
    {
        var registry = CreateRegistry();
        var value = new object();
        registry.Register(ResourceKey.Parse("pangu:first"), value);

        Assert.Throws<InvalidOperationException>(() =>
            registry.Register(ResourceKey.Parse("pangu:second"), value));
    }

    [Fact]
    public void GetKeyAndGetIdRejectNullValue()
    {
        var registry = CreateRegistry();

        Assert.Throws<ArgumentNullException>(() => registry.GetKey(null!));
        Assert.Throws<ArgumentNullException>(() => registry.GetId(null!));
    }

    [Fact]
    public void GetKeyAndGetIdRejectUnregisteredValue()
    {
        var registry = CreateRegistry();

        Assert.Throws<KeyNotFoundException>(() => registry.GetKey(new object()));
        Assert.Throws<KeyNotFoundException>(() => registry.GetId(new object()));
    }

    [Fact]
    public void TryGetKeyAndTryGetIdRejectNullAndUnregisteredValues()
    {
        var registry = CreateRegistry();

        Assert.False(registry.TryGetKey(null!, out var nullKey));
        Assert.Equal(default, nullKey);
        Assert.False(registry.TryGetId(null!, out var nullId));
        Assert.Equal(-1, nullId);
        Assert.False(registry.TryGetKey(new object(), out var missingKey));
        Assert.Equal(default, missingKey);
        Assert.False(registry.TryGetId(new object(), out var missingId));
        Assert.Equal(-1, missingId);
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

    private sealed class ValueEqualResource(int value)
    {
        private int Value { get; } = value;

        public override bool Equals(object? obj) => obj is ValueEqualResource other && Value == other.Value;

        public override int GetHashCode() => Value;
    }

    private static Registry<object> CreateRegistry() => new(ResourceKey.Parse("pangu:test"));
}
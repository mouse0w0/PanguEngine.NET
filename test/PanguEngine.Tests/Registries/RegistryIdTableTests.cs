using PanguEngine.Registries;

namespace PanguEngine.Tests.Registries;

public sealed class RegistryIdTableTests
{
    [Fact]
    public void AllocateUsesRegistrationOrderAndProvidesDirectLookups()
    {
        var first = new object();
        var second = new object();
        var registry = CreateRegistry(("z_first", first), ("a_second", second));

        var table = new RegistryIdTable<object>(registry);
        table.Allocate();

        Assert.Equal(registry.Key, table.Key);
        Assert.Equal(typeof(object), table.ValueType);
        Assert.Equal(2, table.Count);
        Assert.Equal([0, 1], table.Entries.Select(entry => entry.Id).ToArray());
        Assert.Same(first, table.Get(0));
        Assert.Same(second, table.Get(1));
        Assert.Equal(0, table.GetId(first));
        Assert.Equal(0, table.GetId(ResourceKey.Parse("test:z_first")));
        Assert.True(table.TryGetId(ResourceKey.Parse("test:a_second"), out var keyId));
        Assert.Equal(1, keyId);
        Assert.Equal(ResourceKey.Parse("test:a_second"), table.GetKey(1));
        Assert.Equal(ResourceKey.Parse("test:z_first"), table.GetKey(first));
        Assert.True(table.TryGet(1, out var actual));
        Assert.Same(second, actual);
        Assert.True(table.TryGetEntry(0, out var entry));
        Assert.Same(first, entry.Value);
        Assert.True(table.TryGetId(second, out var id));
        Assert.Equal(1, id);
    }

    [Fact]
    public void BindUsesServerKeyOrderAndLocalObjectsRegardlessOfRegistrationOrder()
    {
        var server = CreateRegistry(("z_first", new object()), ("a_second", new object()));
        var first = new object();
        var second = new object();
        var extra = new object();
        var client = CreateRegistry(("extra", extra), ("a_second", second), ("z_first", first));

        var authoritative = new RegistryIdTable<object>(server);
        authoritative.Allocate();
        var mapping = authoritative.ExportMapping();
        var bound = new RegistryIdTable<object>(client);
        bound.Bind(mapping);

        Assert.Same(first, bound.Get(0));
        Assert.Same(second, bound.Get(1));
        Assert.Equal(authoritative.GetKey(0), bound.GetKey(0));
        Assert.Equal(1, bound.GetId(second));
        Assert.Equal(0, bound.GetId(ResourceKey.Parse("test:z_first")));
        Assert.Equal([0, 1], bound.Entries.Select(entry => entry.Id).ToArray());
        Assert.Equal(
            [ResourceKey.Parse("test:z_first"), ResourceKey.Parse("test:a_second")],
            bound.ExportMapping().Keys);
        Assert.False(bound.TryGetEntry(2, out var missing));
        Assert.Equal(default(RegistryIdEntry<object>), missing);
        Assert.False(bound.TryGetId(extra, out var id));
        Assert.Equal(-1, id);
        Assert.Throws<KeyNotFoundException>(() => bound.GetId(extra));
        var extraKey = ResourceKey.Parse("test:extra");
        Assert.False(bound.TryGetId(extraKey, out var extraId));
        Assert.Equal(-1, extraId);
        Assert.Throws<KeyNotFoundException>(() => bound.GetId(extraKey));
    }

    [Fact]
    public void SameTableCanBeAllocatedClearedAndRebound()
    {
        var value = new object();
        var other = new object();
        var registry = CreateRegistry(("value", value), ("other", other));
        var table = new RegistryIdTable<object>(registry);
        Assert.False(table.TryGetId(value, out var missingId));
        Assert.Equal(-1, missingId);

        table.Allocate();
        var exported = table.ExportMapping();
        Assert.Same(value, table.Get(0));
        table.Clear();
        Assert.Empty(table.Entries);
        Assert.False(table.TryGet(0, out _));
        Assert.False(table.TryGetId(value, out _));
        Assert.False(table.TryGetId(ResourceKey.Parse("test:value"), out var clearedId));
        Assert.Equal(-1, clearedId);

        table.Bind(new RegistryIdMapping(registry.Key,
        [
            ResourceKey.Parse("test:other"),
            ResourceKey.Parse("test:value")
        ]));
        Assert.Same(value, table.Get(1));
        Assert.Equal(1, table.GetId(value));
        Assert.Equal(1, table.GetId(ResourceKey.Parse("test:value")));
        Assert.Same(other, table.Get(0));
        Assert.Equal(ResourceKey.Parse("test:value"), exported.Keys[0]);
        Assert.Equal(ResourceKey.Parse("test:value"), registry.GetKey(value));

        table.Allocate();
        Assert.Same(value, table.Get(0));
        Assert.Equal(0, table.GetId(ResourceKey.Parse("test:value")));
        Assert.Equal(1, table.GetId(other));
    }

    [Fact]
    public void ReverseLookupUsesReferenceIdentityAndRejectsMissingValues()
    {
        var registry = new Registry<ValueEqualResource>(ResourceKey.Parse("test:content"));
        var value = new ValueEqualResource(7);
        registry.Register(ResourceKey.Parse("test:value"), value);
        registry.Freeze();
        var table = new RegistryIdTable<ValueEqualResource>(registry);
        table.Allocate();
        var other = new ValueEqualResource(7);

        Assert.Equal(0, table.GetId(value));
        Assert.False(table.TryGetId(other, out var missingId));
        Assert.Equal(-1, missingId);
        Assert.False(table.TryGetId((ValueEqualResource?)null, out var nullId));
        Assert.Equal(-1, nullId);
        Assert.False(table.TryGetId((ResourceKey?)null, out var nullKeyId));
        Assert.Equal(-1, nullKeyId);
        Assert.Throws<KeyNotFoundException>(() => table.GetId(other));
        Assert.Throws<KeyNotFoundException>(() => table.GetKey(other));
        Assert.Throws<ArgumentNullException>(() => table.GetId((ValueEqualResource)null!));
        Assert.Throws<ArgumentNullException>(() => table.GetId((ResourceKey)null!));
        Assert.Throws<ArgumentNullException>(() => table.GetKey((ValueEqualResource)null!));
    }

    [Fact]
    public void DefaultedRegistryDoesNotSupplyMissingDefinitionsOrIds()
    {
        var key = ResourceKey.Parse("test:content");
        var defaultKey = ResourceKey.Parse("test:default");
        var registry = new DefaultedRegistry<object>(key, defaultKey);
        registry.Register(defaultKey, new object());
        registry.Freeze();
        var mapping = new RegistryIdMapping(key, [ResourceKey.Parse("test:missing")]);

        var table = new RegistryIdTable<object>(registry);
        Assert.Throws<KeyNotFoundException>(() => table.Bind(mapping));

        table.Allocate();
        var missingKey = ResourceKey.Parse("test:missing");
        Assert.False(table.TryGetId(missingKey, out var missingId));
        Assert.Equal(-1, missingId);
        Assert.Throws<KeyNotFoundException>(() => table.GetId(missingKey));
        Assert.False(table.TryGet(-1, out var value));
        Assert.Null(value);
        Assert.False(table.TryGetEntry(99, out var entry));
        Assert.Equal(default(RegistryIdEntry<object>), entry);
        Assert.Throws<KeyNotFoundException>(() => table.Get(-1));
        Assert.Throws<KeyNotFoundException>(() => table.GetEntry(99));
        Assert.Throws<KeyNotFoundException>(() => table.GetKey(99));
    }

    [Fact]
    public void UnfrozenRegistryCannotBeAllocatedOrBound()
    {
        var registry = new Registry<object>(ResourceKey.Parse("test:content"));
        var table = new RegistryIdTable<object>(registry);
        var mapping = new RegistryIdMapping(registry.Key, []);

        Assert.Throws<InvalidOperationException>(() => table.Allocate());
        Assert.Throws<InvalidOperationException>(() => table.Bind(mapping));
    }

    [Fact]
    public void BindRejectsDuplicateKeys()
    {
        var registry = CreateRegistry(("first", new object()), ("second", new object()));
        var mapping = new RegistryIdMapping(registry.Key,
        [
            ResourceKey.Parse("test:first"),
            ResourceKey.Parse("test:first")
        ]);

        var table = new RegistryIdTable<object>(registry);
        Assert.Throws<InvalidOperationException>(() => table.Bind(mapping));
    }

    [Fact]
    public void BindRejectsMappingForAnotherRegistry()
    {
        var registry = CreateRegistry();
        var mapping = new RegistryIdMapping(ResourceKey.Parse("test:other"), []);

        var table = new RegistryIdTable<object>(registry);
        Assert.Throws<InvalidOperationException>(() => table.Bind(mapping));
    }

    [Fact]
    public void EmptyMappingLeavesAllLocalDefinitionsUnnumbered()
    {
        var value = new object();
        var registry = CreateRegistry(("value", value));

        var table = new RegistryIdTable<object>(registry);
        table.Allocate();
        table.Bind(new RegistryIdMapping(registry.Key, []));

        Assert.Empty(table.Entries);
        Assert.False(table.TryGetId(value, out _));
        Assert.False(table.TryGetId(ResourceKey.Parse("test:value"), out _));
    }

    private static Registry<object> CreateRegistry(params (string Name, object Value)[] entries)
    {
        var registry = new Registry<object>(ResourceKey.Parse("test:content"));
        foreach (var (name, value) in entries)
            registry.Register(ResourceKey.Create("test", name), value);
        registry.Freeze();
        return registry;
    }

    private sealed record ValueEqualResource(int Value);
}

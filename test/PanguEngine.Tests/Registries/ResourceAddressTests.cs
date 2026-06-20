using PanguEngine.Registries;

namespace PanguEngine.Tests.Registries;

public sealed class ResourceAddressTests
{
    [Fact]
    public void ConstructorStoresRegistryAndEntryKeys()
    {
        var registry = ResourceKey.Parse("pangu:block");
        var entry = ResourceKey.Parse("pangu:stone");

        var address = new ResourceAddress(registry, entry);

        Assert.Equal(registry, address.RegistryKey);
        Assert.Equal(entry, address.EntryKey);
    }

    [Fact]
    public void ConstructorRejectsInvalidKeys()
    {
        var valid = ResourceKey.Parse("pangu:block");

        Assert.Throws<ArgumentException>(() => new ResourceAddress(default, valid));
        Assert.Throws<ArgumentException>(() => new ResourceAddress(valid, default));
    }

    [Fact]
    public void ToStringReturnsReadableAddress()
    {
        var address = new ResourceAddress(
            ResourceKey.Parse("pangu:block"),
            ResourceKey.Parse("pangu:stone"));

        Assert.Equal("pangu:block/pangu:stone", address.ToString());
    }
}
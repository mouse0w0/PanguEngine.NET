using PanguEngine.Registries;

namespace PanguEngine.Tests.Registries;

public sealed class HolderTests
{
    [Fact]
    public void DirectHolderIsBoundImmediately()
    {
        var value = new object();

        var holder = Holder<object>.Direct(value);

        Assert.Null(holder.Address);
        Assert.True(holder.IsBound);
        Assert.Same(value, holder.Value);
        Assert.True(holder.TryGet(out var actual));
        Assert.Same(value, actual);
    }

    [Fact]
    public void DirectRejectsNullValue()
    {
        Assert.Throws<ArgumentNullException>(() => Holder<object>.Direct(null!));
    }
}
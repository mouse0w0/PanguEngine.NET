using PanguEngine.Server;

namespace PanguEngine.Tests.Server;

public sealed class PlayerNamesTests
{
    [Theory]
    [InlineData("")]
    [InlineData("abcdefghijklmnopqrstuvwxyz1234567")]
    [InlineData(" Alice")]
    [InlineData("Alice ")]
    [InlineData("Alice\n")]
    [InlineData("玩家")]
    [InlineData("Alice-Bob")]
    public void RejectsNamesOutsideTheDevelopmentFormat(string name)
    {
        Assert.Throws<InvalidDataException>(() => new PlayerNames().Reserve(name));
    }

    [Theory]
    [InlineData("A")]
    [InlineData("abcdefghijklmnopqrstuvwxyz123456")]
    [InlineData("Alice_123")]
    public void ReleasedNamesCanBeAcceptedAgain(string name)
    {
        var names = new PlayerNames();
        names.Reserve(name);
        Assert.Throws<InvalidDataException>(() => names.Reserve(name.ToUpperInvariant()));
        names.Release(name.ToUpperInvariant());
        names.Reserve(name);
    }

    [Fact]
    public async Task ConcurrentCaseVariantsOnlyAcceptOneIdentity()
    {
        var names = new PlayerNames();
        var results = await Task.WhenAll(Task.Run(() => Accept("Alice")), Task.Run(() => Accept("alice")));
        Assert.Single(results.Where(accepted => accepted));

        bool Accept(string name)
        {
            try
            {
                names.Reserve(name);
                return true;
            }
            catch (InvalidDataException)
            {
                return false;
            }
        }
    }
}

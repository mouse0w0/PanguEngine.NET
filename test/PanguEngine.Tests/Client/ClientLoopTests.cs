using PanguEngine.Client;

namespace PanguEngine.Tests.Client;

public sealed class ClientLoopTests
{
    [Fact]
    public void ContinuationPredicateStopsAfterCurrentIteration()
    {
        var shouldContinue = true;
        var renderCount = 0;
        var loop = new ClientLoop(
            () => shouldContinue,
            () => shouldContinue = false,
            () => { },
            _ => renderCount++);
        loop.UpdatesPerSecond = 0;

        loop.Run();

        Assert.Equal(1, renderCount);
        Assert.False(loop.IsRunning);
    }
}

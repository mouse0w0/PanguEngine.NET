using PanguEngine.Client;

namespace PanguEngine.Tests.Client;

public sealed class ClientLoopTests
{
    [Fact]
    public void RequestStopCompletesCurrentIterationBeforeReturning()
    {
        ClientLoop loop = null!;
        var events = new List<string>();
        loop = new ClientLoop(
            () =>
            {
                events.Add("shouldContinue");
                return true;
            },
            () =>
            {
                events.Add("pumpEvents");
                loop.RequestStop();
            },
            () => events.Add("update"),
            _ => events.Add("render"));
        loop.UpdatesPerSecond = 0;

        loop.Run();

        Assert.Equal(["shouldContinue", "pumpEvents", "render"], events);
        Assert.False(loop.IsRunning);
    }
}

using System.Runtime.ExceptionServices;
using PanguEngine.Client.UI;

namespace PanguEngine.Tests.Client.UI;

public sealed class UiDispatcherTests
{
    [Fact]
    public void ConstructionBindsTheCurrentThread()
    {
        var dispatcher = new UiDispatcher();

        Assert.True(dispatcher.CheckAccess());
        dispatcher.VerifyAccess();

        var backgroundResult = RunOnBackgroundThread(() =>
            (HasAccess: dispatcher.CheckAccess(),
                VerifyError: Record.Exception(dispatcher.VerifyAccess),
                DrainError: Record.Exception(dispatcher.DrainPending)));

        Assert.False(backgroundResult.HasAccess);
        Assert.IsType<InvalidOperationException>(backgroundResult.VerifyError);
        Assert.IsType<InvalidOperationException>(backgroundResult.DrainError);
    }

    [Fact]
    public void DispatchersBoundToTheSameThreadRemainIndependent()
    {
        var first = new UiDispatcher();
        var second = new UiDispatcher();

        first.Shutdown();

        Assert.False(first.CheckAccess());
        Assert.True(second.CheckAccess());
        second.VerifyAccess();
    }

    [Fact]
    public void PostAlwaysQueuesAndDrainPreservesFifo()
    {
        var dispatcher = new UiDispatcher();
        var calls = new List<int>();

        dispatcher.Post(() => calls.Add(1));
        dispatcher.Post(() => calls.Add(2));

        Assert.Empty(calls);
        dispatcher.DrainPending();
        Assert.Equal([1, 2], calls);
        Assert.Throws<ArgumentNullException>(() => dispatcher.Post(null!));
    }

    [Fact]
    public void EmptyDrainIsANoOpAndLeavesTheDispatcherUsable()
    {
        var dispatcher = new UiDispatcher();
        var calls = 0;

        dispatcher.DrainPending();
        dispatcher.Post(() => calls++);
        dispatcher.DrainPending();

        Assert.Equal(1, calls);
    }

    [Fact]
    public void DrainDefersActionsPostedDuringTheCurrentBatch()
    {
        var dispatcher = new UiDispatcher();
        var calls = new List<string>();
        dispatcher.Post(() =>
        {
            calls.Add("first");
            dispatcher.Post(() => calls.Add("deferred"));
        });
        dispatcher.Post(() => calls.Add("second"));

        dispatcher.DrainPending();
        Assert.Equal(["first", "second"], calls);

        dispatcher.DrainPending();
        Assert.Equal(["first", "second", "deferred"], calls);
    }

    [Fact]
    public void MultipleProducersPreservePerProducerOrderAndExecuteExactlyOnce()
    {
        var dispatcher = new UiDispatcher();
        var executed = new List<(int Producer, int Index)>();
        var producers = Enumerable.Range(0, 3)
            .Select(producer => Task.Run(() =>
            {
                for (var index = 0; index < 5; index++)
                {
                    var actionIndex = index;
                    dispatcher.Post(() => executed.Add((producer, actionIndex)));
                }
            }))
            .ToArray();

        Task.WhenAll(producers).GetAwaiter().GetResult();
        dispatcher.DrainPending();

        Assert.Equal(15, executed.Count);
        Assert.Equal(15, executed.Distinct().Count());
        foreach (var producer in Enumerable.Range(0, 3))
        {
            Assert.Equal(
                [0, 1, 2, 3, 4],
                executed.Where(item => item.Producer == producer).Select(item => item.Index));
        }
    }

    [Fact]
    public void ReentrantDrainIsRejectedAndDrainStateRecovers()
    {
        var dispatcher = new UiDispatcher();
        Exception? reentrantError = null;
        var calls = 0;
        dispatcher.Post(() => reentrantError = Record.Exception(dispatcher.DrainPending));
        dispatcher.Post(() => calls++);

        dispatcher.DrainPending();

        Assert.IsType<InvalidOperationException>(reentrantError);
        Assert.Equal(1, calls);
        dispatcher.Post(() => calls++);
        dispatcher.DrainPending();
        Assert.Equal(2, calls);
    }

    [Fact]
    public void ActionExceptionPropagatesAndPreservesRemainingOrder()
    {
        var dispatcher = new UiDispatcher();
        var expected = new InvalidOperationException("action failed");
        var calls = new List<string>();
        dispatcher.Post(() =>
        {
            dispatcher.Post(() => calls.Add("new"));
            throw expected;
        });
        dispatcher.Post(() => calls.Add("remaining"));

        var actual = Assert.Throws<InvalidOperationException>(dispatcher.DrainPending);

        Assert.Same(expected, actual);
        Assert.Empty(calls);
        dispatcher.DrainPending();
        Assert.Equal(["remaining", "new"], calls);
    }

    [Fact]
    public void ShutdownRequiresTheBoundThreadBeforeClosing()
    {
        var dispatcher = new UiDispatcher();

        var backgroundError = RunOnBackgroundThread(() => Record.Exception(dispatcher.Shutdown));

        Assert.IsType<InvalidOperationException>(backgroundError);
        Assert.True(dispatcher.CheckAccess());
    }

    [Fact]
    public void ShutdownDiscardsWorkAndIsIdempotentFromAnyThreadAfterClosing()
    {
        var dispatcher = new UiDispatcher();
        var calls = 0;
        dispatcher.Post(() => calls++);

        dispatcher.Shutdown();
        dispatcher.Shutdown();
        var backgroundResult = RunOnBackgroundThread(() =>
            (ShutdownError: Record.Exception(dispatcher.Shutdown),
                VerifyError: Record.Exception(dispatcher.VerifyAccess),
                DrainError: Record.Exception(dispatcher.DrainPending),
                PostError: Record.Exception(() => dispatcher.Post(() => { }))));

        Assert.Null(backgroundResult.ShutdownError);
        Assert.IsType<ObjectDisposedException>(backgroundResult.VerifyError);
        Assert.IsType<ObjectDisposedException>(backgroundResult.DrainError);
        Assert.IsType<ObjectDisposedException>(backgroundResult.PostError);
        Assert.Equal(0, calls);
        Assert.False(dispatcher.CheckAccess());
        Assert.Throws<ObjectDisposedException>(dispatcher.VerifyAccess);
        Assert.Throws<ObjectDisposedException>(dispatcher.DrainPending);
        Assert.Throws<ObjectDisposedException>(() => dispatcher.Post(() => { }));
    }

    [Fact]
    public void ShutdownInsideActionCompletesCurrentActionAndDiscardsTheRest()
    {
        var dispatcher = new UiDispatcher();
        var calls = new List<string>();
        Exception? postError = null;
        Exception? drainError = null;
        dispatcher.Post(() =>
        {
            calls.Add("current");
            dispatcher.Shutdown();
            postError = Record.Exception(() => dispatcher.Post(() => { }));
            drainError = Record.Exception(dispatcher.DrainPending);
            calls.Add("completed");
        });
        dispatcher.Post(() => calls.Add("discarded"));

        dispatcher.DrainPending();

        Assert.Equal(["current", "completed"], calls);
        Assert.IsType<ObjectDisposedException>(postError);
        Assert.IsType<InvalidOperationException>(drainError);
    }

    [Fact]
    public void PostAndShutdownHonorTheirLinearizedOrder()
    {
        var acceptedFirst = new UiDispatcher();
        var calls = 0;
        RunOnBackgroundThread(() => acceptedFirst.Post(() => calls++));
        acceptedFirst.Shutdown();
        Assert.Equal(0, calls);

        var shutdownFirst = new UiDispatcher();
        shutdownFirst.Shutdown();
        var rejectedError = RunOnBackgroundThread(() =>
            Record.Exception(() => shutdownFirst.Post(() => { })));
        Assert.IsType<ObjectDisposedException>(rejectedError);
    }

    private static void RunOnBackgroundThread(Action action) =>
        RunOnBackgroundThread(() =>
        {
            action();
            return true;
        });

    private static T RunOnBackgroundThread<T>(Func<T> action)
    {
        T result = default!;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception exception)
            {
                error = exception;
            }
        });
        thread.Start();
        thread.Join();

        if (error is not null)
            ExceptionDispatchInfo.Capture(error).Throw();

        return result;
    }
}
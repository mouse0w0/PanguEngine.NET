using PanguEngine.Threading;

namespace PanguEngine.Tests.Threading;

public sealed class EngineWorkQueueTests
{
    [Fact]
    public void RunPendingUsesFixedFifoBatch()
    {
        var queue = new EngineWorkQueue();
        var values = new List<int>();
        queue.PostContinuation(() => values.Add(1));
        queue.PostContinuation(() => values.Add(2));

        queue.RunPending();

        Assert.Equal([1, 2], values);
    }

    [Fact]
    public void WorkAddedDuringDrainWaitsForNextBatch()
    {
        var queue = new EngineWorkQueue();
        var values = new List<int>();
        queue.PostContinuation(() =>
        {
            values.Add(1);
            queue.PostContinuation(() => values.Add(2));
        });

        queue.RunPending();

        Assert.Equal([1], values);
        Assert.Equal(1, queue.PendingCount);

        queue.RunPending();

        Assert.Equal([1, 2], values);
    }

    [Fact]
    public void ReentrantDrainIsRejectedAndStateRecovers()
    {
        var queue = new EngineWorkQueue();
        Exception? reentrantError = null;
        queue.PostContinuation(() => reentrantError = Record.Exception(queue.RunPending));

        queue.RunPending();
        queue.PostContinuation(() => { });
        var recoveryError = Record.Exception(queue.RunPending);

        Assert.IsType<InvalidOperationException>(reentrantError);
        Assert.Null(recoveryError);
    }

    [Fact]
    public void CallbackFailurePropagatesAndLeavesRemainingCallbacksQueued()
    {
        var queue = new EngineWorkQueue();
        var expected = new InvalidOperationException("continuation");
        var secondRan = false;
        queue.PostContinuation(() => throw expected);
        queue.PostContinuation(() => secondRan = true);

        var actual = Assert.Throws<InvalidOperationException>(queue.RunPending);

        Assert.Same(expected, actual);
        Assert.False(secondRan);
        Assert.Equal(1, queue.PendingCount);

        queue.RunPending();

        Assert.True(secondRan);
    }

    [Fact]
    public void ContinuationAfterDestroyIsDropped()
    {
        var queue = new EngineWorkQueue();
        var callbackRan = false;

        queue.Destroy();
        queue.Destroy();
        queue.PostContinuation(() => callbackRan = true);

        Assert.False(callbackRan);
        Assert.Equal(0, queue.PendingCount);
        Assert.Throws<ObjectDisposedException>(queue.RunPending);
    }

    [Fact]
    public void TryPostOperationQueuesWorkAndRegistersAbortCallback()
    {
        var queue = new EngineWorkQueue();
        var ran = false;
        var aborted = false;

        Assert.True(queue.TryPostOperation(() => ran = true, () => aborted = true));
        Assert.Equal(1, queue.PendingCount);
        Assert.False(aborted);

        queue.RunPending();

        Assert.True(ran);
        Assert.False(aborted);
    }

    [Fact]
    public void TryPostOperationAfterDestroyReturnsFalse()
    {
        var queue = new EngineWorkQueue();
        queue.Destroy();

        Assert.False(queue.TryPostOperation(() => { }, () => { }));
        Assert.Equal(0, queue.PendingCount);
    }

    [Fact]
    public void DestroyAbortsRegisteredOperationsAndClearsQueue()
    {
        var queue = new EngineWorkQueue();
        var ran = false;
        var abortCount = 0;

        Assert.True(queue.TryPostOperation(() => ran = true, () => abortCount++));
        Assert.True(queue.TryPostOperation(() => ran = true, () => abortCount++));

        queue.Destroy();

        Assert.False(ran);
        Assert.Equal(2, abortCount);
        Assert.Equal(0, queue.PendingCount);
    }

    [Fact]
    public void CompleteOperationPreventsAbort()
    {
        var queue = new EngineWorkQueue();
        var abortCount = 0;
        Action abort = () => abortCount++;

        Assert.True(queue.TryPostOperation(() => { }, abort));
        queue.CompleteOperation(abort);

        queue.Destroy();

        Assert.Equal(0, abortCount);
    }

    [Fact]
    public void SameAbortCallbackInstanceCanBeRegisteredTwice()
    {
        var queue = new EngineWorkQueue();
        var abortCount = 0;
        Action abort = () => abortCount++;

        Assert.True(queue.TryPostOperation(() => { }, abort));
        Assert.True(queue.TryPostOperation(() => { }, abort));
        queue.CompleteOperation(abort);

        queue.Destroy();

        Assert.Equal(1, abortCount);
    }

    [Fact]
    public void DestroyAbortsRegisteredOperationsOnlyOnce()
    {
        var queue = new EngineWorkQueue();
        var abortCount = 0;

        Assert.True(queue.TryPostOperation(() => { }, () => abortCount++));

        queue.Destroy();
        queue.Destroy();

        Assert.Equal(1, abortCount);
    }

    [Fact]
    public void RunPendingFromAnotherThreadIsRejected()
    {
        var queue = new EngineWorkQueue();
        Exception? failure = null;
        var thread = new Thread(() => failure = Record.Exception(queue.RunPending));

        thread.Start();
        thread.Join();

        Assert.IsType<InvalidOperationException>(failure);
    }

    [Fact]
    public void DestroyFromAnotherThreadIsRejected()
    {
        var queue = new EngineWorkQueue();
        Exception? failure = null;
        var thread = new Thread(() => failure = Record.Exception(queue.Destroy));

        thread.Start();
        thread.Join();

        Assert.IsType<InvalidOperationException>(failure);
    }

    [Fact]
    public void IsOwnerThreadReportsOwningThread()
    {
        var queue = new EngineWorkQueue();

        Assert.True(queue.IsOwnerThread);

        var otherThreadResult = true;
        var thread = new Thread(() => otherThreadResult = queue.IsOwnerThread);
        thread.Start();
        thread.Join();

        Assert.False(otherThreadResult);
    }
}

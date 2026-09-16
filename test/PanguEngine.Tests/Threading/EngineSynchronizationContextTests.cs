using PanguEngine.Threading;

namespace PanguEngine.Tests.Threading;

public sealed class EngineSynchronizationContextTests
{
    [Fact]
    public void PostUsesFixedFifoBatch()
    {
        var queue = new EngineWorkQueue();
        var context = new EngineSynchronizationContext(queue);
        var values = new List<int>();
        context.Post(_ => values.Add(1), null);
        context.Post(_ => values.Add(2), null);

        queue.RunPending();

        Assert.Equal([1, 2], values);
    }

    [Fact]
    public void PostsCreatedDuringDrainWaitForNextBatch()
    {
        var queue = new EngineWorkQueue();
        var context = new EngineSynchronizationContext(queue);
        var values = new List<int>();
        context.Post(_ =>
        {
            values.Add(1);
            context.Post(_ => values.Add(2), null);
        }, null);

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
        var context = new EngineSynchronizationContext(queue);
        Exception? reentrantError = null;
        context.Post(_ => reentrantError = Record.Exception(queue.RunPending), null);

        queue.RunPending();
        context.Post(_ => { }, null);
        var recoveryError = Record.Exception(queue.RunPending);

        Assert.IsType<InvalidOperationException>(reentrantError);
        Assert.Null(recoveryError);
    }

    [Fact]
    public void CallbackFailurePropagatesAndLeavesRemainingCallbacksQueued()
    {
        var queue = new EngineWorkQueue();
        var context = new EngineSynchronizationContext(queue);
        var expected = new InvalidOperationException("continuation");
        var secondRan = false;
        context.Post(_ => throw expected, null);
        context.Post(_ => secondRan = true, null);

        var actual = Assert.Throws<InvalidOperationException>(queue.RunPending);

        Assert.Same(expected, actual);
        Assert.False(secondRan);
        Assert.Equal(1, queue.PendingCount);

        queue.RunPending();

        Assert.True(secondRan);
    }

    [Fact]
    public void AwaitFromOwnerThreadResumesOnOwnerThread()
    {
        var queue = new EngineWorkQueue();
        var context = new EngineSynchronizationContext(queue);
        var previous = SynchronizationContext.Current;
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ownerThreadId = Environment.CurrentManagedThreadId;
        var resumedThreadId = 0;
        SynchronizationContext.SetSynchronizationContext(context);

        try
        {
            var awaiting = AwaitCompletion();
            Task.Run(source.SetResult).GetAwaiter().GetResult();
            Assert.True(SpinWait.SpinUntil(() => queue.PendingCount == 1, TimeSpan.FromSeconds(5)));

            queue.RunPending();

            Assert.True(awaiting.IsCompletedSuccessfully);
            Assert.Equal(ownerThreadId, resumedThreadId);
        }
        finally
        {
            queue.Destroy();
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        async Task AwaitCompletion()
        {
            await source.Task;
            resumedThreadId = Environment.CurrentManagedThreadId;
        }
    }

    [Fact]
    public void ConfigureAwaitFalseDoesNotQueueContinuationToOwnerThread()
    {
        var queue = new EngineWorkQueue();
        var context = new EngineSynchronizationContext(queue);
        var previous = SynchronizationContext.Current;
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ownerThreadId = Environment.CurrentManagedThreadId;
        var resumedThreadId = 0;
        SynchronizationContext.SetSynchronizationContext(context);

        try
        {
            var awaiting = AwaitCompletion();
            Task.Run(source.SetResult).GetAwaiter().GetResult();
            Assert.True(SpinWait.SpinUntil(() => awaiting.IsCompleted, TimeSpan.FromSeconds(5)));

            Assert.True(awaiting.IsCompletedSuccessfully);
            Assert.Equal(0, queue.PendingCount);
            Assert.NotEqual(ownerThreadId, resumedThreadId);
        }
        finally
        {
            queue.Destroy();
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        async Task AwaitCompletion()
        {
            await source.Task.ConfigureAwait(false);
            resumedThreadId = Environment.CurrentManagedThreadId;
        }
    }

    [Fact]
    public void AwaitStartedOnBackgroundThreadDoesNotQueueContinuationToOwnerThread()
    {
        var queue = new EngineWorkQueue();
        var context = new EngineSynchronizationContext(queue);
        var previous = SynchronizationContext.Current;
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var started = new ManualResetEventSlim();
        SynchronizationContext.SetSynchronizationContext(context);

        try
        {
            var awaiting = Task.Run(async () =>
            {
                started.Set();
                await source.Task;
            });
            Assert.True(started.Wait(TimeSpan.FromSeconds(5)));

            source.SetResult();
            Assert.True(SpinWait.SpinUntil(() => awaiting.IsCompleted, TimeSpan.FromSeconds(5)));

            Assert.True(awaiting.IsCompletedSuccessfully);
            Assert.Equal(0, queue.PendingCount);
        }
        finally
        {
            queue.Destroy();
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    [Fact]
    public void CreateCopyReturnsSameContext()
    {
        var queue = new EngineWorkQueue();
        var context = new EngineSynchronizationContext(queue);

        Assert.Same(context, context.CreateCopy());
    }

    [Fact]
    public void SendRunsSynchronouslyOnOwnerThread()
    {
        var queue = new EngineWorkQueue();
        var context = new EngineSynchronizationContext(queue);
        var ownerThreadId = Environment.CurrentManagedThreadId;
        var callbackThreadId = 0;

        context.Send(_ => callbackThreadId = Environment.CurrentManagedThreadId, null);

        Assert.Equal(ownerThreadId, callbackThreadId);
        Assert.Equal(0, queue.PendingCount);
    }

    [Fact]
    public void SendFromAnotherThreadIsRejected()
    {
        var queue = new EngineWorkQueue();
        var context = new EngineSynchronizationContext(queue);
        Exception? failure = null;
        var thread = new Thread(() => failure = Record.Exception(() => context.Send(_ => { }, null)));

        thread.Start();
        thread.Join();

        Assert.IsType<InvalidOperationException>(failure);
    }

    [Fact]
    public void DestroyClearsPendingCallbacksAndDropsLaterPosts()
    {
        var queue = new EngineWorkQueue();
        var context = new EngineSynchronizationContext(queue);
        var callbackRan = false;
        context.Post(_ => { }, null);

        queue.Destroy();
        queue.Destroy();
        context.Post(_ => callbackRan = true, null);

        Assert.Equal(0, queue.PendingCount);
        Assert.False(callbackRan);
        Assert.Throws<ObjectDisposedException>(queue.RunPending);
    }

}

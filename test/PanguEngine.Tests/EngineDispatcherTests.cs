using PanguEngine.Threading;

namespace PanguEngine.Tests;

public sealed class EngineDispatcherTests
{
    [Fact]
    public void CheckAccessReportsOwningThread()
    {
        var queue = new EngineWorkQueue();
        var dispatcher = new EngineDispatcher(queue);

        Assert.True(dispatcher.CheckAccess());

        var otherThreadResult = true;
        var thread = new Thread(() => otherThreadResult = dispatcher.CheckAccess());
        thread.Start();
        thread.Join();

        Assert.False(otherThreadResult);
    }

    [Fact]
    public async Task InvokeAsyncActionRunsOnOwningThread()
    {
        var queue = new EngineWorkQueue();
        var dispatcher = new EngineDispatcher(queue);
        var ownerThreadId = Environment.CurrentManagedThreadId;
        var ranThreadId = 0;

        Action action = () => ranThreadId = Environment.CurrentManagedThreadId;
        var task = dispatcher.InvokeAsync(action);
        queue.RunPending();

        await task;

        Assert.Equal(ownerThreadId, ranThreadId);
    }

    [Fact]
    public async Task InvokeAsyncActionFromOwningThreadStillQueues()
    {
        var queue = new EngineWorkQueue();
        var dispatcher = new EngineDispatcher(queue);
        var ran = false;

        Action action = () => ran = true;
        var task = dispatcher.InvokeAsync(action);

        Assert.False(ran);
        Assert.Equal(1, queue.PendingCount);

        queue.RunPending();

        await task;

        Assert.True(ran);
    }

    [Fact]
    public async Task InvokeAsyncFunctionReturnsResult()
    {
        var queue = new EngineWorkQueue();
        var dispatcher = new EngineDispatcher(queue);

        Func<int> function = () => 42;
        var task = dispatcher.InvokeAsync(function);
        queue.RunPending();

        Assert.Equal(42, await task);
    }

    [Fact]
    public async Task InvokeAsyncFunctionExceptionFaultsTaskWithoutBreakingLoop()
    {
        var queue = new EngineWorkQueue();
        var dispatcher = new EngineDispatcher(queue);
        var expected = new InvalidOperationException("scheduled");
        var secondRan = false;

        Func<int> function = () => throw expected;
        var task = dispatcher.InvokeAsync(function);
        queue.PostContinuation(() => secondRan = true);
        queue.RunPending();

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Same(expected, actual);
        Assert.True(secondRan);
    }

    [Fact]
    public async Task InvokeAsyncAsyncFunctionCompletesAfterInnerTask()
    {
        var queue = new EngineWorkQueue();
        var dispatcher = new EngineDispatcher(queue);
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ran = false;

        Func<Task> function = async () =>
        {
            await source.Task;
            ran = true;
        };
        var task = dispatcher.InvokeAsync(function);
        queue.RunPending();

        Assert.False(task.IsCompleted);
        Assert.False(ran);

        source.SetResult();

        await task;

        Assert.True(ran);
    }

    [Fact]
    public async Task InvokeAsyncAsyncFunctionReturnsResult()
    {
        var queue = new EngineWorkQueue();
        var dispatcher = new EngineDispatcher(queue);
        var source = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        Func<Task<int>> function = () => source.Task;
        var task = dispatcher.InvokeAsync(function);
        queue.RunPending();

        source.SetResult(7);

        Assert.Equal(7, await task);
    }

    [Fact]
    public async Task InvokeAsyncAsyncFunctionExceptionFaultsTask()
    {
        var queue = new EngineWorkQueue();
        var dispatcher = new EngineDispatcher(queue);
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var expected = new InvalidOperationException("async scheduled");

        Func<Task> function = () => source.Task;
        var task = dispatcher.InvokeAsync(function);
        queue.RunPending();

        source.SetException(expected);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task InvokeAsyncAsyncFunctionSynchronousThrowFaultsTask()
    {
        var queue = new EngineWorkQueue();
        var dispatcher = new EngineDispatcher(queue);
        var expected = new InvalidOperationException("start");

        Func<Task> function = () => throw expected;
        var task = dispatcher.InvokeAsync(function);
        queue.RunPending();

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task InvokeAsyncAfterDestroyReturnsFaultedTask()
    {
        var queue = new EngineWorkQueue();
        var dispatcher = new EngineDispatcher(queue);
        queue.Destroy();

        Action action = () => { };
        var task = dispatcher.InvokeAsync(action);

        Assert.True(task.IsFaulted);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => task);
    }

    [Fact]
    public async Task DestroyAbortsPendingOperations()
    {
        var queue = new EngineWorkQueue();
        var dispatcher = new EngineDispatcher(queue);
        Action first = () => { };
        Action second = () => { };

        var firstTask = dispatcher.InvokeAsync(first);
        var secondTask = dispatcher.InvokeAsync(second);
        Assert.False(firstTask.IsCompleted);
        Assert.False(secondTask.IsCompleted);

        queue.Destroy();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => firstTask);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => secondTask);
    }

    [Fact]
    public async Task DestroyAbortsInFlightAsyncOperation()
    {
        var queue = new EngineWorkQueue();
        var dispatcher = new EngineDispatcher(queue);
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Func<Task> function = () => source.Task;
        var task = dispatcher.InvokeAsync(function);
        queue.RunPending();
        Assert.False(task.IsCompleted);

        queue.Destroy();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => task);

        source.SetResult();
        Assert.True(task.IsFaulted);
    }

    [Fact]
    public async Task DestroyDoesNotAbortFinishedOperation()
    {
        var queue = new EngineWorkQueue();
        var dispatcher = new EngineDispatcher(queue);
        var ran = false;

        Action action = () => ran = true;
        var task = dispatcher.InvokeAsync(action);
        queue.RunPending();

        queue.Destroy();

        Assert.True(ran);
        await task;
    }

    [Fact]
    public void NullDelegateThrowsArgumentNullException()
    {
        var queue = new EngineWorkQueue();
        var dispatcher = new EngineDispatcher(queue);

        Assert.Throws<ArgumentNullException>(() => { _ = dispatcher.InvokeAsync((Action)null!); });
        Assert.Throws<ArgumentNullException>(() => { _ = dispatcher.InvokeAsync((Func<int>)null!); });
        Assert.Throws<ArgumentNullException>(() => { _ = dispatcher.InvokeAsync((Func<Task>)null!); });
        Assert.Throws<ArgumentNullException>(() => { _ = dispatcher.InvokeAsync((Func<Task<int>>)null!); });
    }
}

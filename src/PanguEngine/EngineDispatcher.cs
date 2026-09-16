using PanguEngine.Threading;

namespace PanguEngine;

/// <summary>
/// Schedules work onto the owning engine loop thread.
/// </summary>
/// <remarks>
/// Work is always queued and never executed inline, even when scheduled from the owning thread.
/// Do not block on the returned task from the owning thread; that deadlocks the loop. Exceptions from
/// scheduled delegates are reported through the returned task and never propagate into the loop.
/// Shutdown does not drain pending work: work that has not started is aborted, and its task fails with
/// <see cref="ObjectDisposedException"/>.
/// </remarks>
public sealed class EngineDispatcher
{
    private readonly EngineWorkQueue _queue;

    internal EngineDispatcher(EngineWorkQueue queue) => _queue = queue;

    /// <summary>
    /// Determines whether the calling thread is the owning engine loop thread.
    /// </summary>
    /// <returns><see langword="true"/> when the calling thread owns the engine loop.</returns>
    public bool CheckAccess() => _queue.IsOwnerThread;

    /// <summary>
    /// Schedules an action and returns a task that completes when the action has run.
    /// </summary>
    /// <param name="action">The action to run on the engine loop.</param>
    /// <returns>A task that completes when the action finishes.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
    public Task InvokeAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var abortCallback = CreateAbortCallback(completion);
        if (!_queue.TryPostOperation(() =>
        {
            try
            {
                action();
                completion.TrySetResult();
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
            finally
            {
                _queue.CompleteOperation(abortCallback);
            }
        }, abortCallback))
        {
            return Task.FromException(new ObjectDisposedException(nameof(EngineDispatcher)));
        }

        return completion.Task;
    }

    /// <summary>
    /// Schedules a function and returns a task for its result.
    /// </summary>
    /// <typeparam name="TResult">The result type of the scheduled function.</typeparam>
    /// <param name="function">The function to run on the engine loop.</param>
    /// <returns>A task that completes with the function result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="function"/> is null.</exception>
    public Task<TResult> InvokeAsync<TResult>(Func<TResult> function)
    {
        ArgumentNullException.ThrowIfNull(function);
        var completion = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var abortCallback = CreateAbortCallback(completion);
        if (!_queue.TryPostOperation(() =>
        {
            try
            {
                completion.TrySetResult(function());
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
            finally
            {
                _queue.CompleteOperation(abortCallback);
            }
        }, abortCallback))
        {
            return Task.FromException<TResult>(new ObjectDisposedException(nameof(EngineDispatcher)));
        }

        return completion.Task;
    }

    /// <summary>
    /// Schedules an asynchronous function and returns a task that completes when it finishes.
    /// </summary>
    /// <param name="function">The asynchronous function to start on the engine loop.</param>
    /// <returns>A task that completes when the asynchronous function finishes.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="function"/> is null.</exception>
    public Task InvokeAsync(Func<Task> function)
    {
        ArgumentNullException.ThrowIfNull(function);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var abortCallback = CreateAbortCallback(completion);
        if (!_queue.TryPostOperation(() =>
        {
            try
            {
                Observe(function(), completion, _queue, abortCallback);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
                _queue.CompleteOperation(abortCallback);
            }
        }, abortCallback))
        {
            return Task.FromException(new ObjectDisposedException(nameof(EngineDispatcher)));
        }

        return completion.Task;
    }

    /// <summary>
    /// Schedules an asynchronous function and returns a task for its result.
    /// </summary>
    /// <typeparam name="TResult">The result type of the asynchronous function.</typeparam>
    /// <param name="function">The asynchronous function to start on the engine loop.</param>
    /// <returns>A task that completes with the function result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="function"/> is null.</exception>
    public Task<TResult> InvokeAsync<TResult>(Func<Task<TResult>> function)
    {
        ArgumentNullException.ThrowIfNull(function);
        var completion = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var abortCallback = CreateAbortCallback(completion);
        if (!_queue.TryPostOperation(() =>
        {
            try
            {
                Observe(function(), completion, _queue, abortCallback);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
                _queue.CompleteOperation(abortCallback);
            }
        }, abortCallback))
        {
            return Task.FromException<TResult>(new ObjectDisposedException(nameof(EngineDispatcher)));
        }

        return completion.Task;
    }

    private static Action CreateAbortCallback(TaskCompletionSource completion) =>
        () => completion.TrySetException(new ObjectDisposedException(nameof(EngineDispatcher)));

    private static Action CreateAbortCallback<TResult>(TaskCompletionSource<TResult> completion) =>
        () => completion.TrySetException(new ObjectDisposedException(nameof(EngineDispatcher)));

    private static void Observe(Task inner, TaskCompletionSource completion, EngineWorkQueue queue, Action abortCallback)
    {
        inner.ContinueWith(
            _ =>
            {
                try
                {
                    inner.GetAwaiter().GetResult();
                    completion.TrySetResult();
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
                finally
                {
                    queue.CompleteOperation(abortCallback);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static void Observe<TResult>(
        Task<TResult> inner,
        TaskCompletionSource<TResult> completion,
        EngineWorkQueue queue,
        Action abortCallback)
    {
        inner.ContinueWith(
            _ =>
            {
                try
                {
                    completion.TrySetResult(inner.GetAwaiter().GetResult());
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
                finally
                {
                    queue.CompleteOperation(abortCallback);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}

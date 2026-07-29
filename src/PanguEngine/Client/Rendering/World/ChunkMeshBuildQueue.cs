using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using System.Threading.Channels;
using PanguEngine.Client.Resources.Models;
using PanguEngine.World.Chunking;

namespace PanguEngine.Client.Rendering.World;

internal sealed record ChunkMeshBuildTask(
    ChunkMeshSnapshot Snapshot,
    long Version);

internal sealed record ChunkMeshBuildResult(
    ChunkPos Position,
    long Version,
    ChunkMesh? Mesh,
    ExceptionDispatchInfo? Exception)
{
    internal static ChunkMeshBuildResult Success(
        ChunkMeshBuildTask task,
        ChunkMesh mesh) =>
        new(task.Snapshot.Position, task.Version, mesh, null);

    internal static ChunkMeshBuildResult Failure(
        ChunkMeshBuildTask task,
        Exception exception) =>
        new(task.Snapshot.Position, task.Version, null, ExceptionDispatchInfo.Capture(exception));
}

internal sealed class ChunkMeshBuildQueue : IDisposable
{
    private readonly ChunkMeshBuilder _builder;
    private readonly Channel<ChunkMeshBuildTask> _tasks;
    private readonly Channel<ChunkMeshBuildResult> _results;
    private readonly SemaphoreSlim _taskSlots;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task[] _workers;
    private ExceptionDispatchInfo? _workerFailure;
    private bool _destroyed;

    internal ChunkMeshBuildQueue(BlockModelManager models)
        : this(models, GetDefaultWorkerCount())
    {
    }

    private ChunkMeshBuildQueue(BlockModelManager models, int workerCount)
        : this(models, workerCount, workerCount * 2, workerCount * 2)
    {
    }

    internal ChunkMeshBuildQueue(
        BlockModelManager models,
        int workerCount,
        int taskCapacity,
        int resultCapacity)
    {
        _builder = new ChunkMeshBuilder(models);
        _tasks = Channel.CreateBounded<ChunkMeshBuildTask>(new BoundedChannelOptions(taskCapacity)
        {
            SingleWriter = true,
            SingleReader = workerCount == 1,
            FullMode = BoundedChannelFullMode.Wait
        });
        _results = Channel.CreateBounded<ChunkMeshBuildResult>(new BoundedChannelOptions(resultCapacity)
        {
            SingleWriter = workerCount == 1,
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait
        });
        _taskSlots = new SemaphoreSlim(taskCapacity, taskCapacity);
        DispatchBudget = taskCapacity;
        _workers = new Task[workerCount];
        for (var index = 0; index < _workers.Length; index++)
            _workers[index] = Task.Run(RunWorkerAsync);
    }

    internal int DispatchBudget { get; }

    internal bool TryReserveTaskSlot()
    {
        return !_destroyed && _taskSlots.Wait(0);
    }

    internal void ReleaseReservedTaskSlot()
    {
        _taskSlots.Release();
    }

    internal bool TryEnqueueReserved(ChunkMeshBuildTask task)
    {
        return !_destroyed && _tasks.Writer.TryWrite(task);
    }

    internal bool TryReadResult([NotNullWhen(true)] out ChunkMeshBuildResult? result)
    {
        return _results.Reader.TryRead(out result);
    }

    internal void ThrowIfFaulted()
    {
        Volatile.Read(ref _workerFailure)?.Throw();
    }

    public void Dispose()
    {
        if (_destroyed)
            return;

        _destroyed = true;
        _tasks.Writer.TryComplete();
        _cancellation.Cancel();
        Task.WhenAll(_workers).GetAwaiter().GetResult();
        _results.Writer.TryComplete();
        while (_tasks.Reader.TryRead(out _))
        {
        }

        while (_results.Reader.TryRead(out _))
        {
        }

        _taskSlots.Dispose();
        _cancellation.Dispose();
    }

    private static int GetDefaultWorkerCount()
    {
        return Math.Clamp(Environment.ProcessorCount - 1, 1, 4);
    }

    private async Task RunWorkerAsync()
    {
        try
        {
            await foreach (var task in _tasks.Reader.ReadAllAsync(_cancellation.Token))
            {
                _taskSlots.Release();
                ChunkMeshBuildResult result;
                try
                {
                    result = ChunkMeshBuildResult.Success(task, _builder.Build(task.Snapshot));
                }
                catch (Exception exception)
                {
                    result = ChunkMeshBuildResult.Failure(task, exception);
                }

                await _results.Writer.WriteAsync(result, _cancellation.Token);
            }
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Interlocked.CompareExchange(
                ref _workerFailure,
                ExceptionDispatchInfo.Capture(exception),
                null);
            await _cancellation.CancelAsync();
        }
    }
}
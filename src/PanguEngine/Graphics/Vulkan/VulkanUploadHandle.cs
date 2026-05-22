using System.Runtime.ExceptionServices;

namespace PanguEngine.Graphics.Vulkan;

internal sealed class VulkanUploadHandle : UploadHandle
{
    private readonly ManualResetEventSlim _event = new(false);
    private volatile bool _completed;
    private ExceptionDispatchInfo? _exception;

    public override bool IsCompleted => _completed;

    public override bool IsFaulted => _exception != null;

    public override Exception? Exception => _exception?.SourceException;

    internal void SignalSuccess()
    {
        _completed = true;
        _event.Set();
    }

    internal void SignalFailure(Exception exception)
    {
        _exception = ExceptionDispatchInfo.Capture(exception);
        _completed = true;
        _event.Set();
    }

    public override void Wait()
    {
        if (VulkanUploader.IsRenderSubmitThread)
            throw new InvalidOperationException(
                "Cannot wait on an UploadHandle from the render submit thread; this would cause a deadlock.");

        _event.Wait();

        _exception?.Throw();
    }
}
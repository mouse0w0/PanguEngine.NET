namespace PanguEngine.Graphics.Vulkan;

internal sealed class VulkanUploadHandle : UploadHandle
{
    private volatile UploadState _state;
    private volatile Exception? _exception;

    protected override UploadState State => _state;

    public override Exception? Exception => _exception;

    /// <summary>
    /// Marks the upload as ready to be consumed by a subsequent graphics submission.
    /// </summary>
    internal void SignalReady()
    {
        if (_state == UploadState.Pending)
            _state = UploadState.Ready;
    }

    internal void SignalSuccess()
    {
        _state = UploadState.Succeeded;
    }

    internal void SignalFailure(Exception exception)
    {
        _exception = exception;
        _state = UploadState.Faulted;
    }
}

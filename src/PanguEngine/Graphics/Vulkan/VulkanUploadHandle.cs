namespace PanguEngine.Graphics.Vulkan;

internal sealed class VulkanUploadHandle : UploadHandle
{
    private volatile bool _completed;
    private Exception? _exception;

    public override bool IsCompleted => _completed;

    public override bool IsFaulted => _completed && _exception != null;

    public override Exception? Exception => _exception;

    internal void SignalSuccess()
    {
        _completed = true;
    }

    internal void SignalFailure(Exception exception)
    {
        _exception = exception;
        _completed = true;
    }
}
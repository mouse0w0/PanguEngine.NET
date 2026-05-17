namespace PanguEngine.Graphics.Vulkan;

internal sealed class VulkanGraphicsUploadHandle : GraphicsUploadHandle
{
    private readonly VulkanUploader.UploadHandle _handle;

    public VulkanGraphicsUploadHandle(VulkanUploader.UploadHandle handle)
    {
        _handle = handle ?? throw new ArgumentNullException(nameof(handle));
    }

    public override bool IsCompleted => _handle.IsCompleted;

    public override bool IsFaulted => _handle.IsFaulted;

    public override Exception? Exception => _handle.Exception;

    public override void Wait()
    {
        _handle.Wait();
    }
}
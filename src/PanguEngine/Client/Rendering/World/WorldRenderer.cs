using PanguEngine.Client.World;
using PanguEngine.Graphics;

namespace PanguEngine.Client.Rendering.World;

/// <summary>
/// Coordinates world rendering for a presentation target.
/// </summary>
internal sealed class WorldRenderer
{
    private readonly GraphicsDevice _device;
    private readonly Presenter _presenter;
    private readonly ChunkRenderer _chunkRenderer;

    /// <summary>
    /// Creates a world renderer.
    /// </summary>
    /// <param name="device">The graphics device.</param>
    /// <param name="presenter">The presentation target.</param>
    /// <param name="world">The client world to render.</param>
    public WorldRenderer(GraphicsDevice device, Presenter presenter, ClientWorld world)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        _chunkRenderer = new ChunkRenderer(_device, _presenter.ColorFormat, world);
    }

    /// <summary>
    /// Draws a world frame.
    /// </summary>
    /// <param name="alpha">The interpolation factor between fixed updates.</param>
    public void DrawFrame(double alpha)
    {
        var uploadHandles = _chunkRenderer.RebuildDirtyChunks();
        if (!_presenter.TryBeginFrame(out var frame))
            return;

        var activeFrame = frame!;
        InvalidOperationException? uploadFailure = null;
        try
        {
            uploadFailure = GetUploadFailure(uploadHandles);

            var commandList = activeFrame.CommandList;
            commandList.Begin();
            commandList.BeginRendering(new RenderingDescription(new[]
            {
                new ColorAttachmentDescription(activeFrame.ColorOutput, new ClearColor(0.008f, 0.01f, 0.016f, 1)),
            }));
            commandList.SetViewport(0, 0, activeFrame.Width, activeFrame.Height);
            commandList.SetScissor(0, 0, activeFrame.Width, activeFrame.Height);

            if (uploadFailure is null)
                _chunkRenderer.Draw(commandList);

            commandList.EndRendering();
            commandList.PrepareForPresent();
            commandList.End();
        }
        finally
        {
            _presenter.EndFrame(activeFrame);
        }

        if (uploadFailure is not null)
            throw uploadFailure;
    }

    /// <summary>
    /// Releases resources owned by this renderer.
    /// </summary>
    public void Destroy()
    {
        _device.WaitIdle();
        _chunkRenderer.Destroy();
    }

    /// <summary>
    /// Gets an upload failure from a set of upload handles.
    /// </summary>
    /// <param name="uploadHandles">The upload handles to inspect.</param>
    /// <returns>The upload failure, or null when all uploads completed successfully.</returns>
    private static InvalidOperationException? GetUploadFailure(List<UploadHandle> uploadHandles)
    {
        foreach (var uploadHandle in uploadHandles)
        {
            if (uploadHandle.IsFaulted)
                return new InvalidOperationException("World chunk mesh upload failed.", uploadHandle.Exception);
            if (!uploadHandle.IsCompleted)
            {
                return new InvalidOperationException(
                    "World chunk mesh upload did not complete after flushing pending uploads.");
            }
        }

        return null;
    }
}
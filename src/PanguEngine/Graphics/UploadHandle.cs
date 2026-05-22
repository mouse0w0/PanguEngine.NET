namespace PanguEngine.Graphics;

/// <summary>
/// Represents the completion state of a queued graphics upload request.
/// </summary>
public abstract class UploadHandle
{
    /// <summary>
    /// Gets whether the upload has completed successfully or failed.
    /// </summary>
    public abstract bool IsCompleted { get; }

    /// <summary>
    /// Gets whether the upload completed with an error.
    /// </summary>
    public abstract bool IsFaulted { get; }

    /// <summary>
    /// Gets the error that caused the upload to fail, if any.
    /// </summary>
    public abstract Exception? Exception { get; }

    /// <summary>
    /// Blocks the calling thread until the upload completes.
    /// </summary>
    public abstract void Wait();
}
using System.Runtime.ExceptionServices;

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
    /// Gets whether the upload has completed without an error.
    /// </summary>
    public bool IsCompletedSuccessfully => IsCompleted && !IsFaulted;

    /// <summary>
    /// Gets the error that caused the upload to fail, if any.
    /// </summary>
    public abstract Exception? Exception { get; }

    /// <summary>
    /// Checks whether the upload has completed without an error, and throws the upload error when it has failed.
    /// </summary>
    /// <returns><see langword="true" /> when the upload completed successfully; <see langword="false" /> when it has not completed.</returns>
    /// <exception cref="Exception">Thrown when the upload completed with an error; the thrown exception is the upload error.</exception>
    public bool CheckSuccess()
    {
        if (!IsCompleted)
            return false;

        if (IsFaulted)
        {
            ExceptionDispatchInfo.Capture(Exception!).Throw();
        }

        return true;
    }
}
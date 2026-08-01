using System.Runtime.ExceptionServices;

namespace PanguEngine.Graphics;

/// <summary>
/// Represents the state of a queued graphics upload request.
/// </summary>
public abstract class UploadHandle
{
    /// <summary>
    /// Represents the current state of a queued graphics upload request.
    /// </summary>
    protected enum UploadState
    {
        /// <summary>
        /// The upload has been enqueued but is not yet ready to be consumed by a graphics submission.
        /// </summary>
        Pending,

        /// <summary>
        /// The upload data is ready to be consumed by a subsequent graphics submission.
        /// </summary>
        Ready,

        /// <summary>
        /// The upload has succeeded and the host has observed its completion.
        /// </summary>
        Succeeded,

        /// <summary>
        /// The upload failed and its data can no longer be consumed.
        /// </summary>
        Faulted
    }

    /// <summary>
    /// Gets the current state of the upload request as a single snapshot.
    /// </summary>
    protected abstract UploadState State { get; }

    /// <summary>
    /// Gets whether the upload data is ready to be consumed by a subsequent graphics submission.
    /// </summary>
    /// <remarks>
    /// Readiness is established by the backend scheduling contract and does not imply that the host has observed
    /// completion. A later uploader fault revokes readiness even after it was established.
    /// </remarks>
    public bool IsReady => State is UploadState.Ready or UploadState.Succeeded;

    /// <summary>
    /// Gets whether the host has observed the upload complete, either successfully or with an error.
    /// </summary>
    /// <remarks>
    /// Host completion is independent of readiness: a succeeded upload is still ready to be consumed, while a faulted
    /// upload is complete even though its data can no longer be consumed.
    /// </remarks>
    public bool IsCompleted => State is UploadState.Succeeded or UploadState.Faulted;

    /// <summary>
    /// Gets whether the upload has failed.
    /// </summary>
    public bool IsFaulted => State == UploadState.Faulted;

    /// <summary>
    /// Gets whether the host has observed the upload complete without an error.
    /// </summary>
    public bool IsSucceeded => State == UploadState.Succeeded;

    /// <summary>
    /// Gets the error that caused the upload to fail, if any.
    /// </summary>
    public abstract Exception? Exception { get; }

    /// <summary>
    /// Throws when the upload data is not ready to be consumed by a subsequent graphics submission.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the upload is still pending.</exception>
    /// <exception cref="Exception">Thrown when the upload failed; the thrown exception is the recorded upload error.</exception>
    public void ThrowIfNotReady()
    {
        var state = State;
        if (state == UploadState.Faulted)
            ExceptionDispatchInfo.Capture(Exception!).Throw();
        if (state == UploadState.Pending)
            throw new InvalidOperationException("The upload is pending and cannot be consumed by a graphics submission yet.");
    }
}

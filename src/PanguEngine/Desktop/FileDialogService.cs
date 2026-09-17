using PanguEngine.Windowing;

namespace PanguEngine.Desktop;

/// <summary>
/// Provides asynchronous access to platform file and folder dialogs while client services are active.
/// </summary>
/// <remarks>
/// Operations must be started on the client main thread and must not be synchronously blocked with
/// <see cref="Task.Wait()"/> or <see cref="Task{TResult}.Result"/> on that thread. A returned task may
/// already be complete because the platform can report a result before the operation returns.
/// </remarks>
public abstract class FileDialogService
{
    private readonly int _mainThreadId = Environment.CurrentManagedThreadId;
    private bool _destroyed;

    internal FileDialogService()
    {
    }

    /// <summary>
    /// Shows a dialog for selecting one file.
    /// </summary>
    /// <param name="options">Platform dialog hints, or <see langword="null"/> for defaults.</param>
    /// <returns>The selected path, or <see langword="null"/> when the user cancels.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown synchronously for an invalid calling thread or owner, or asynchronously for a platform failure.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when this service or the owner is destroyed.</exception>
    public Task<string?> OpenFileAsync(FileDialogOptions? options = null)
    {
        var snapshot = Snapshot(options);
        return OpenFileCoreAsync(snapshot);
    }

    /// <summary>
    /// Shows a dialog for selecting multiple files.
    /// </summary>
    /// <param name="options">Platform dialog hints, or <see langword="null"/> for defaults.</param>
    /// <returns>The selected paths, or an empty list when the user cancels.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown synchronously for an invalid calling thread or owner, or asynchronously for a platform failure.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when this service or the owner is destroyed.</exception>
    public Task<IReadOnlyList<string>> OpenFilesAsync(FileDialogOptions? options = null)
    {
        var snapshot = Snapshot(options);
        return OpenFilesCoreAsync(snapshot);
    }

    /// <summary>
    /// Shows a dialog for choosing a file path to save.
    /// </summary>
    /// <param name="options">Platform dialog hints, or <see langword="null"/> for defaults.</param>
    /// <returns>The selected path, or <see langword="null"/> when the user cancels.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown synchronously for an invalid calling thread or owner, or asynchronously for a platform failure.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when this service or the owner is destroyed.</exception>
    public Task<string?> SaveFileAsync(FileDialogOptions? options = null)
    {
        var snapshot = Snapshot(options);
        return SaveFileCoreAsync(snapshot);
    }

    /// <summary>
    /// Shows a dialog for selecting one folder.
    /// </summary>
    /// <param name="options">Platform dialog hints, or <see langword="null"/> for defaults.</param>
    /// <returns>The selected path, or <see langword="null"/> when the user cancels.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown synchronously for an invalid calling thread or owner, or asynchronously for a platform failure.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when this service or the owner is destroyed.</exception>
    public Task<string?> OpenFolderAsync(FolderDialogOptions? options = null)
    {
        var snapshot = Snapshot(options);
        return OpenFolderCoreAsync(snapshot);
    }

    /// <summary>
    /// Shows a dialog for selecting multiple folders.
    /// </summary>
    /// <param name="options">Platform dialog hints, or <see langword="null"/> for defaults.</param>
    /// <returns>The selected paths, or an empty list when the user cancels.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown synchronously for an invalid calling thread or owner, or asynchronously for a platform failure.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when this service or the owner is destroyed.</exception>
    public Task<IReadOnlyList<string>> OpenFoldersAsync(FolderDialogOptions? options = null)
    {
        var snapshot = Snapshot(options);
        return OpenFoldersCoreAsync(snapshot);
    }

    internal bool HasPendingNativeRequests => HasPendingNativeRequestsCore;

    internal void Destroy()
    {
        if (_destroyed)
            return;

        EnsureMainThread();
        _destroyed = true;
        DestroyCore();
    }

    /// <summary>Shows a platform dialog that selects one file.</summary>
    /// <param name="options">The validated snapshot of the caller options.</param>
    /// <returns>The selected path, or <see langword="null"/> when the user cancels.</returns>
    protected abstract Task<string?> OpenFileCoreAsync(FileDialogOptions options);

    /// <summary>Shows a platform dialog that selects multiple files.</summary>
    /// <param name="options">The validated snapshot of the caller options.</param>
    /// <returns>The selected paths, or an empty list when the user cancels.</returns>
    protected abstract Task<IReadOnlyList<string>> OpenFilesCoreAsync(FileDialogOptions options);

    /// <summary>Shows a platform dialog that chooses a file path to save.</summary>
    /// <param name="options">The validated snapshot of the caller options.</param>
    /// <returns>The selected path, or <see langword="null"/> when the user cancels.</returns>
    protected abstract Task<string?> SaveFileCoreAsync(FileDialogOptions options);

    /// <summary>Shows a platform dialog that selects one folder.</summary>
    /// <param name="options">The validated snapshot of the caller options.</param>
    /// <returns>The selected path, or <see langword="null"/> when the user cancels.</returns>
    protected abstract Task<string?> OpenFolderCoreAsync(FolderDialogOptions options);

    /// <summary>Shows a platform dialog that selects multiple folders.</summary>
    /// <param name="options">The validated snapshot of the caller options.</param>
    /// <returns>The selected paths, or an empty list when the user cancels.</returns>
    protected abstract Task<IReadOnlyList<string>> OpenFoldersCoreAsync(FolderDialogOptions options);

    /// <summary>Gets whether any native dialog is still waiting for its platform callback.</summary>
    protected abstract bool HasPendingNativeRequestsCore { get; }

    /// <summary>Releases platform resources and fails outstanding requests. Called once on the client main thread.</summary>
    protected abstract void DestroyCore();

    private FileDialogOptions Snapshot(FileDialogOptions? options)
    {
        EnsureAvailable(options?.Owner);
        if (options is null)
            return new FileDialogOptions();

        return new FileDialogOptions
        {
            Owner = options.Owner,
            Title = options.Title,
            InitialLocation = options.InitialLocation,
            Filters = options.Filters.ToArray()
        };
    }

    private FolderDialogOptions Snapshot(FolderDialogOptions? options)
    {
        EnsureAvailable(options?.Owner);
        if (options is null)
            return new FolderDialogOptions();

        return new FolderDialogOptions
        {
            Owner = options.Owner,
            Title = options.Title,
            InitialLocation = options.InitialLocation
        };
    }

    private void EnsureAvailable(Window? owner)
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
        EnsureMainThread();
        if (owner is not null)
            ObjectDisposedException.ThrowIf(owner.IsDestroyed, owner);
    }

    private void EnsureMainThread()
    {
        if (_mainThreadId != Environment.CurrentManagedThreadId)
            throw new InvalidOperationException("File dialog operations must run on the client main thread.");
    }
}

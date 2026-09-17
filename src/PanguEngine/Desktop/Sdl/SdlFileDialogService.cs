using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using PanguEngine.Windowing;
using SDL;

namespace PanguEngine.Desktop.Sdl;

internal sealed class SdlFileDialogService : FileDialogService
{
    private readonly Lock _sync = new();
    private readonly HashSet<PendingRequest> _pending = [];
    private bool _stopping;

    protected override bool HasPendingNativeRequestsCore
    {
        get
        {
            lock (_sync)
                return _pending.Count != 0;
        }
    }

    protected override Task<string?> OpenFileCoreAsync(FileDialogOptions options) =>
        SelectSingleAsync(ShowAsync(
            SDL_FileDialogType.SDL_FILEDIALOG_OPENFILE,
            options.Owner,
            options.Title,
            options.InitialLocation,
            false,
            options.Filters));

    protected override Task<IReadOnlyList<string>> OpenFilesCoreAsync(FileDialogOptions options) =>
        SelectManyAsync(ShowAsync(
            SDL_FileDialogType.SDL_FILEDIALOG_OPENFILE,
            options.Owner,
            options.Title,
            options.InitialLocation,
            true,
            options.Filters));

    protected override Task<string?> SaveFileCoreAsync(FileDialogOptions options) =>
        SelectSingleAsync(ShowAsync(
            SDL_FileDialogType.SDL_FILEDIALOG_SAVEFILE,
            options.Owner,
            options.Title,
            options.InitialLocation,
            false,
            options.Filters));

    protected override Task<string?> OpenFolderCoreAsync(FolderDialogOptions options) =>
        SelectSingleAsync(ShowAsync(
            SDL_FileDialogType.SDL_FILEDIALOG_OPENFOLDER,
            options.Owner,
            options.Title,
            options.InitialLocation,
            false,
            []));

    protected override Task<IReadOnlyList<string>> OpenFoldersCoreAsync(FolderDialogOptions options) =>
        SelectManyAsync(ShowAsync(
            SDL_FileDialogType.SDL_FILEDIALOG_OPENFOLDER,
            options.Owner,
            options.Title,
            options.InitialLocation,
            true,
            []));

    protected override void DestroyCore()
    {
        lock (_sync)
        {
            _stopping = true;
            foreach (var request in _pending)
            {
                request.Completion.TrySetException(
                    new ObjectDisposedException(nameof(FileDialogService), "The file dialog service is shutting down."));
            }
        }
    }

    private Task<string[]> ShowAsync(
        SDL_FileDialogType type,
        Window? owner,
        string? title,
        string? initialLocation,
        bool allowMany,
        IReadOnlyList<FileDialogFilter> filters)
    {
        var ownerHandle = GetOwnerHandle(owner);
        var pending = new PendingRequest(owner);
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_stopping, this);
            _pending.Add(pending);
        }

        try
        {
            ShowNative(type, ownerHandle, title, initialLocation, allowMany, filters, pending);
        }
        catch (Exception exception)
        {
            Complete(pending, null, exception);
            Finish(pending);
        }

        return pending.Completion.Task;
    }

    private unsafe void ShowNative(
        SDL_FileDialogType type,
        nint ownerHandle,
        string? title,
        string? initialLocation,
        bool allowMany,
        IReadOnlyList<FileDialogFilter> filters,
        PendingRequest pending)
    {
        var state = new NativeRequestState(this, pending);
        SDL_PropertiesID properties = default;
        GCHandle handle = default;
        var callbackOwnsState = false;
        try
        {
            state.AllocateFilters(filters);
            properties = CreateProperties(ownerHandle, title, initialLocation, allowMany, state);
            handle = GCHandle.Alloc(state);

            SDL3.SDL_ShowFileDialogWithProperties(type, &OnCompleted, GCHandle.ToIntPtr(handle), properties);
            callbackOwnsState = true;
        }
        catch
        {
            if (!callbackOwnsState)
            {
                state.FreeNativeFilters();
                if (handle.IsAllocated)
                    handle.Free();
            }

            throw;
        }
        finally
        {
            if (properties != default)
                SDL3.SDL_DestroyProperties(properties);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe void OnCompleted(nint userdata, byte** fileList, int selectedFilter)
    {
        NativeRequestState? state = null;
        GCHandle handle = default;
        try
        {
            handle = GCHandle.FromIntPtr(userdata);
            state = (NativeRequestState)handle.Target!;
            var (paths, error) = ReadCompletion(fileList);
            state.Complete(paths, error);
        }
        catch (Exception exception)
        {
            state?.Fail(exception);
        }
        finally
        {
            state?.Finish();
            TryFreeHandle(handle);
        }
    }

    private void Complete(PendingRequest pending, string[]? paths, Exception? error)
    {
        lock (_sync)
        {
            if (!_pending.Contains(pending) || _stopping)
                return;

            if (error is not null)
                pending.Completion.TrySetException(error);
            else
                pending.Completion.TrySetResult(paths!);
        }
    }

    private void Finish(PendingRequest pending)
    {
        lock (_sync)
            _pending.Remove(pending);
    }

    private static unsafe (string[]? Paths, Exception? Error) ReadCompletion(byte** fileList)
    {
        if (fileList is null)
            return (null, CreateSdlException("SDL file dialog"));
        if (*fileList is null)
            return ([], null);

        var paths = new List<string>();
        for (var current = fileList; *current is not null; current++)
        {
            var nativePath = *current;
            var path = Marshal.PtrToStringUTF8((nint)nativePath);
            if (path is null)
                throw new InvalidOperationException("SDL file dialog returned a path that is not valid UTF-8.");
            paths.Add(path);
        }

        return (paths.ToArray(), null);
    }

    private static unsafe SDL_PropertiesID CreateProperties(
        nint ownerHandle,
        string? title,
        string? initialLocation,
        bool allowMany,
        NativeRequestState state)
    {
        SDL3.SDL_ClearError();
        var properties = SDL3.SDL_CreateProperties();
        if (properties == default)
            throw CreateSdlException("SDL file dialog properties creation");

        try
        {
            if (state.Filters is not null)
            {
                SetPointerProperty(properties, SDL3.SDL_PROP_FILE_DIALOG_FILTERS_POINTER, (nint)state.Filters);
                SetNumberProperty(properties, SDL3.SDL_PROP_FILE_DIALOG_NFILTERS_NUMBER, state.FilterCount);
            }

            if (ownerHandle != 0)
                SetPointerProperty(properties, SDL3.SDL_PROP_FILE_DIALOG_WINDOW_POINTER, ownerHandle);
            if (initialLocation is not null)
                SetStringProperty(properties, SDL3.SDL_PROP_FILE_DIALOG_LOCATION_STRING, initialLocation);
            if (allowMany)
                SetBooleanProperty(properties, SDL3.SDL_PROP_FILE_DIALOG_MANY_BOOLEAN, true);
            if (title is not null)
                SetStringProperty(properties, SDL3.SDL_PROP_FILE_DIALOG_TITLE_STRING, title);

            return properties;
        }
        catch
        {
            SDL3.SDL_DestroyProperties(properties);
            throw;
        }
    }

    private static unsafe void SetPointerProperty(SDL_PropertiesID properties, ReadOnlySpan<byte> name, nint value)
    {
        fixed (byte* nativeName = name)
        {
            if (!SDL3.SDL_SetPointerProperty(properties, nativeName, value))
                throw CreateSdlException("SDL file dialog pointer property update");
        }
    }

    private static unsafe void SetNumberProperty(SDL_PropertiesID properties, ReadOnlySpan<byte> name, long value)
    {
        fixed (byte* nativeName = name)
        {
            if (!SDL3.SDL_SetNumberProperty(properties, nativeName, value))
                throw CreateSdlException("SDL file dialog number property update");
        }
    }

    private static unsafe void SetBooleanProperty(SDL_PropertiesID properties, ReadOnlySpan<byte> name, bool value)
    {
        fixed (byte* nativeName = name)
        {
            if (!SDL3.SDL_SetBooleanProperty(properties, nativeName, value))
                throw CreateSdlException("SDL file dialog boolean property update");
        }
    }

    private static unsafe void SetStringProperty(SDL_PropertiesID properties, ReadOnlySpan<byte> name, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value + '\0');
        fixed (byte* nativeName = name)
        fixed (byte* nativeValue = bytes)
        {
            if (!SDL3.SDL_SetStringProperty(properties, nativeName, nativeValue))
                throw CreateSdlException("SDL file dialog string property update");
        }
    }

    private static InvalidOperationException CreateSdlException(string operation)
    {
        var error = SDL3.SDL_GetError();
        return new InvalidOperationException(
            string.IsNullOrEmpty(error) ? $"{operation} failed." : $"{operation} failed: {error}");
    }

    private static nint GetOwnerHandle(Window? owner)
    {
        if (owner is null)
            return 0;
        if (owner is not ISdlWindow sdlWindow)
            throw new InvalidOperationException("The file dialog owner is not an SDL window.");
        return sdlWindow.Handle;
    }

    private static void TryFreeHandle(GCHandle handle)
    {
        try
        {
            if (handle.IsAllocated)
                handle.Free();
        }
        catch
        {
        }
    }

    private static async Task<string?> SelectSingleAsync(Task<string[]> task)
    {
        var paths = await task.ConfigureAwait(false);
        return paths.Length == 0 ? null : paths[0];
    }

    private static async Task<IReadOnlyList<string>> SelectManyAsync(Task<string[]> task)
    {
        var paths = await task.ConfigureAwait(false);
        return Array.AsReadOnly(paths);
    }

    private sealed class PendingRequest(Window? owner)
    {
        internal TaskCompletionSource<string[]> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Keeps the owner window alive until the native dialog reports back.</summary>
        internal Window? Owner { get; } = owner;
    }

    private sealed unsafe class NativeRequestState(SdlFileDialogService service, PendingRequest pending)
    {
        internal SDL_DialogFileFilter* Filters { get; private set; }

        internal int FilterCount { get; private set; }

        internal void Complete(string[]? paths, Exception? error) =>
            service.Complete(pending, paths, error);

        internal void Fail(Exception exception) =>
            service.Complete(pending, null, exception);

        internal void Finish()
        {
            TryFreeNativeFilters();
            service.Finish(pending);
        }

        internal void AllocateFilters(IReadOnlyList<FileDialogFilter> filters)
        {
            if (filters.Count == 0)
                return;

            FilterCount = filters.Count;
            Filters = (SDL_DialogFileFilter*)NativeMemory.AllocZeroed(
                (nuint)FilterCount,
                (nuint)sizeof(SDL_DialogFileFilter));
            try
            {
                for (var i = 0; i < FilterCount; i++)
                {
                    var filter = filters[i];
                    Filters[i].name = AllocateUtf8(filter.Name);
                    Filters[i].pattern = AllocateUtf8(string.Join(";", filter.Extensions));
                }
            }
            catch
            {
                FreeNativeFilters();
                throw;
            }
        }

        private void TryFreeNativeFilters()
        {
            try
            {
                FreeNativeFilters();
            }
            catch
            {
            }
        }

        internal void FreeNativeFilters()
        {
            if (Filters is null)
                return;

            for (var i = 0; i < FilterCount; i++)
            {
                NativeMemory.Free(Filters[i].name);
                NativeMemory.Free(Filters[i].pattern);
            }

            NativeMemory.Free(Filters);
            Filters = null;
            FilterCount = 0;
        }

        private static byte* AllocateUtf8(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value + '\0');
            var result = (byte*)NativeMemory.Alloc((nuint)bytes.Length);
            bytes.CopyTo(new Span<byte>(result, bytes.Length));
            return result;
        }
    }
}

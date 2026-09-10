using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using PanguEngine.Client;
using SDL;

namespace PanguEngine.Desktop.Sdl;

internal sealed unsafe class SdlClipboard : Clipboard
{
    private const string AlternateTextMimeType = "text/plain";

    private readonly int _mainThreadId;

    internal SdlClipboard()
    {
        _mainThreadId = Environment.CurrentManagedThreadId;
    }

    protected override IReadOnlyList<ClipboardFormat> GetFormatsCore()
    {
        EnsureMainThread();
        SDL3.SDL_ClearError();
        nuint count = 0;
        var mimeTypes = SDL3.SDL_GetClipboardMimeTypes(&count);
        if (mimeTypes is null)
        {
            throw CreateSdlException("SDL clipboard format query", SDL3.SDL_GetError());
        }

        try
        {
            var formats = new List<ClipboardFormat>(checked((int)count));
            var uniqueFormats = new HashSet<ClipboardFormat>();
            for (nuint i = 0; i < count; i++)
            {
                var mimeType = Marshal.PtrToStringUTF8((nint)mimeTypes[i])!;
                var format = IsTextMimeType(mimeType)
                    ? ClipboardFormat.Text
                    : new ClipboardFormat(mimeType);
                if (uniqueFormats.Add(format))
                {
                    formats.Add(format);
                }
            }

            return formats;
        }
        finally
        {
            SDL3.SDL_free((nint)mimeTypes);
        }
    }

    protected override bool TryGetTextCore(out string text)
    {
        EnsureMainThread();
        SDL3.SDL_ClearError();
        var nativeText = SDL3.Unsafe_SDL_GetClipboardText();
        if (nativeText is null)
        {
            throw CreateSdlException("SDL clipboard text read", SDL3.SDL_GetError());
        }

        try
        {
            text = Marshal.PtrToStringUTF8((nint)nativeText) ?? string.Empty;
            if (text.Length != 0)
            {
                return true;
            }

            ThrowIfSdlError("SDL clipboard text read");
            return false;
        }
        finally
        {
            SDL3.SDL_free((nint)nativeText);
        }
    }

    protected override bool TryGetDataCore(ClipboardFormat format, out byte[] data)
    {
        EnsureMainThread();
        if (format == ClipboardFormat.Text)
        {
            if (TryGetTextCore(out var text))
            {
                data = Encoding.UTF8.GetBytes(text);
                return true;
            }

            data = [];
            return false;
        }

        if (!GetFormatsCore().Contains(format))
        {
            data = [];
            return false;
        }

        var mimeTypeBytes = Encoding.UTF8.GetBytes(format.MimeType + '\0');
        fixed (byte* mimeType = mimeTypeBytes)
        {
            SDL3.SDL_ClearError();
            nuint size = 0;
            var nativeData = SDL3.SDL_GetClipboardData(mimeType, &size);
            if (nativeData == 0)
            {
                ThrowIfSdlError("SDL clipboard data read");
                data = [];
                return false;
            }

            try
            {
                data = new byte[checked((int)size)];
                if (data.Length != 0)
                {
                    Marshal.Copy(nativeData, data, 0, data.Length);
                }

                return true;
            }
            finally
            {
                SDL3.SDL_free(nativeData);
            }
        }
    }

    protected override void SetTextCore(string text)
    {
        EnsureMainThread();
        var textBytes = Encoding.UTF8.GetBytes(text + '\0');
        fixed (byte* nativeText = textBytes)
        {
            SDL3.SDL_ClearError();
            if (!SDL3.SDL_SetClipboardText(nativeText))
            {
                var error = SDL3.SDL_GetError();
                SDL3.SDL_ClearClipboardData();
                throw CreateSdlException("SDL clipboard text write", error);
            }
        }
    }

    protected override void SetContentCore(ClipboardContent content)
    {
        EnsureMainThread();
        var entries = content.CopyEntries()
            .OrderBy(static entry => entry.Format == ClipboardFormat.Text ? 0 : 1)
            .ToArray();
        var snapshot = CreateSnapshot(entries);

        SDL3.SDL_ClearError();
        if (SDL3.SDL_SetClipboardData(
                &ReadSnapshotData,
                &FreeSnapshotCallback,
                (nint)snapshot,
                snapshot->MimeTypes,
                snapshot->Count))
        {
            return;
        }

        var error = SDL3.SDL_GetError();
        SDL3.SDL_ClearClipboardData();
        throw CreateSdlException("SDL clipboard data write", error);
    }

    protected override void ClearCore()
    {
        EnsureMainThread();
        SDL3.SDL_ClearError();
        if (!SDL3.SDL_ClearClipboardData())
        {
            throw CreateSdlException("SDL clipboard clear", SDL3.SDL_GetError());
        }
    }

    protected override void DestroyCore()
    {
        EnsureMainThread();
        SDL3.SDL_ClearError();
        if (!SDL3.SDL_ClearClipboardData())
        {
            throw CreateSdlException("SDL clipboard cleanup", SDL3.SDL_GetError());
        }
    }

    private static NativeClipboardSnapshot* CreateSnapshot(IReadOnlyList<ClipboardContentEntry> entries)
    {
        var snapshot = (NativeClipboardSnapshot*)NativeMemory.AllocZeroed((nuint)sizeof(NativeClipboardSnapshot));
        try
        {
            snapshot->Count = (nuint)entries.Count;
            snapshot->Entries = (NativeClipboardEntry*)NativeMemory.AllocZeroed(
                snapshot->Count,
                (nuint)sizeof(NativeClipboardEntry));
            snapshot->MimeTypes = (byte**)NativeMemory.AllocZeroed(snapshot->Count, (nuint)sizeof(byte*));

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var mimeType = Encoding.UTF8.GetBytes(entry.Format.MimeType + '\0');
                var nativeEntry = &snapshot->Entries[i];
                nativeEntry->MimeType = (byte*)NativeMemory.Alloc((nuint)mimeType.Length);
                nativeEntry->Data = (byte*)NativeMemory.Alloc((nuint)Math.Max(entry.Data.Length, 1));

                mimeType.CopyTo(new Span<byte>(nativeEntry->MimeType, mimeType.Length));
                entry.Data.CopyTo(new Span<byte>(nativeEntry->Data, entry.Data.Length));
                nativeEntry->Length = (nuint)entry.Data.Length;
                snapshot->MimeTypes[i] = nativeEntry->MimeType;
            }

            return snapshot;
        }
        catch
        {
            FreeSnapshot(snapshot);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static nint ReadSnapshotData(nint userdata, byte* mimeType, nuint* size)
    {
        if (size is not null)
        {
            *size = 0;
        }

        if (userdata == 0 || mimeType is null || size is null)
        {
            return 0;
        }

        var snapshot = (NativeClipboardSnapshot*)userdata;
        for (nuint i = 0; i < snapshot->Count; i++)
        {
            var entry = &snapshot->Entries[i];
            if (MimeTypesEqual(entry->MimeType, mimeType))
            {
                *size = entry->Length;
                return (nint)entry->Data;
            }
        }

        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void FreeSnapshotCallback(nint userdata)
    {
        FreeSnapshot((NativeClipboardSnapshot*)userdata);
    }

    private static bool MimeTypesEqual(byte* left, byte* right)
    {
        while (*left == *right)
        {
            if (*left == 0)
            {
                return true;
            }

            left++;
            right++;
        }

        return false;
    }

    private static void FreeSnapshot(NativeClipboardSnapshot* snapshot)
    {
        if (snapshot is null)
        {
            return;
        }

        if (snapshot->Entries is not null)
        {
            for (nuint i = 0; i < snapshot->Count; i++)
            {
                NativeMemory.Free(snapshot->Entries[i].MimeType);
                NativeMemory.Free(snapshot->Entries[i].Data);
            }
        }

        NativeMemory.Free(snapshot->MimeTypes);
        NativeMemory.Free(snapshot->Entries);
        NativeMemory.Free(snapshot);
    }

    private void EnsureMainThread()
    {
        if (Environment.CurrentManagedThreadId != _mainThreadId)
        {
            throw new InvalidOperationException("SDL clipboard operations must run on the SDL main thread.");
        }
    }

    private static bool IsTextMimeType(string mimeType)
    {
        return mimeType == ClipboardFormat.Text.MimeType || mimeType == AlternateTextMimeType;
    }

    private static void ThrowIfSdlError(string operation)
    {
        var error = SDL3.SDL_GetError();
        if (!string.IsNullOrEmpty(error))
        {
            throw CreateSdlException(operation, error);
        }
    }

    private static InvalidOperationException CreateSdlException(string operation, string? error) =>
        new(string.IsNullOrEmpty(error) ? $"{operation} failed." : $"{operation} failed: {error}");

    private struct NativeClipboardEntry
    {
        internal byte* MimeType;
        internal byte* Data;
        internal nuint Length;
    }

    private struct NativeClipboardSnapshot
    {
        internal nuint Count;
        internal NativeClipboardEntry* Entries;
        internal byte** MimeTypes;
    }
}

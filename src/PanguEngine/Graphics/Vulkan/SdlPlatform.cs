using System.Runtime.InteropServices;
using System.Text;
using PanguEngine.Windowing;
using SDL;
using Silk.NET.Vulkan;

namespace PanguEngine.Graphics.Vulkan;

internal sealed unsafe class SdlPlatform
{
    private readonly Dictionary<SDL_WindowID, VulkanWindow> _windows = [];
    private readonly Dictionary<SDL_WindowID, nint> _nativeWindows = [];
    private readonly Dictionary<CursorShape, nint> _cursors = [];
    private bool _initialized;
    private bool _quitRequested;

    internal void Initialize()
    {
        VulkanContext.EnsureRenderThread();
        if (!SDL3.SDL_InitSubSystem(SDL_InitFlags.SDL_INIT_VIDEO))
            throw CreateSdlException("SDL video initialization");

        _initialized = true;
        if (!SDL3.SDL_Vulkan_LoadLibrary((byte*)null))
        {
            SDL3.SDL_QuitSubSystem(SDL_InitFlags.SDL_INIT_VIDEO);
            _initialized = false;
            throw CreateSdlException("SDL Vulkan loader initialization");
        }
    }

    internal SDL_Window* CreateWindow(WindowOptions options)
    {
        VulkanContext.EnsureRenderThread();

        var flags = SDL_WindowFlags.SDL_WINDOW_VULKAN |
                    SDL_WindowFlags.SDL_WINDOW_HIGH_PIXEL_DENSITY;
        if (!options.IsVisible)
            flags |= SDL_WindowFlags.SDL_WINDOW_HIDDEN;
        if (options.WindowBorder == WindowBorder.Hidden)
            flags |= SDL_WindowFlags.SDL_WINDOW_BORDERLESS;
        else if (options.WindowBorder == WindowBorder.Resizable)
            flags |= SDL_WindowFlags.SDL_WINDOW_RESIZABLE;
        if (options.TopMost)
            flags |= SDL_WindowFlags.SDL_WINDOW_ALWAYS_ON_TOP;
        if (options.WindowState == WindowState.Minimized)
            flags |= SDL_WindowFlags.SDL_WINDOW_MINIMIZED;
        if (options.WindowState == WindowState.Maximized)
            flags |= SDL_WindowFlags.SDL_WINDOW_MAXIMIZED;
        if (options.WindowState == WindowState.Fullscreen)
            flags |= SDL_WindowFlags.SDL_WINDOW_FULLSCREEN;

        var titleBytes = Encoding.UTF8.GetBytes(options.Title + "\0");
        SDL_Window* window;
        fixed (byte* title = titleBytes)
        {
            window = SDL3.SDL_CreateWindow(title, options.Size.X, options.Size.Y, flags);
        }

        if (window is null)
            throw CreateSdlException("SDL window creation");

        var id = SDL3.SDL_GetWindowID(window);
        _nativeWindows.Add(id, (nint)window);

        try
        {
            SetWindowPosition(window, options.Position);
            if (options.WindowState == WindowState.Fullscreen && options.VideoMode != VideoMode.Default)
                SetFullscreenVideoMode(window, options.VideoMode);
            SyncWindow(window);
            return window;
        }
        catch
        {
            if (_nativeWindows.Remove(id, out var nativeWindow))
                SDL3.SDL_DestroyWindow((SDL_Window*)nativeWindow);
            throw;
        }
    }

    internal void RegisterWindow(VulkanWindow window)
    {
        VulkanContext.EnsureRenderThread();
        var id = SDL3.SDL_GetWindowID(window.NativeWindow);
        _windows.Add(id, window);
    }

    internal void UnregisterAndDestroyWindow(VulkanWindow window)
    {
        VulkanContext.EnsureRenderThread();
        var id = window.WindowId;
        _windows.Remove(id);
        if (_nativeWindows.Remove(id, out var nativeWindow))
            SDL3.SDL_DestroyWindow((SDL_Window*)nativeWindow);
    }

    internal void DestroyWindow(SDL_Window* window)
    {
        VulkanContext.EnsureRenderThread();
        var id = SDL3.SDL_GetWindowID(window);
        if (_nativeWindows.Remove(id, out var nativeWindow))
            SDL3.SDL_DestroyWindow((SDL_Window*)nativeWindow);
    }

    internal static string[] GetVulkanInstanceExtensions()
    {
        VulkanContext.EnsureRenderThread();
        uint count = 0;
        var extensions = SDL3.SDL_Vulkan_GetInstanceExtensions(&count);
        if (extensions is null)
            throw CreateSdlException("SDL Vulkan instance extension query");

        var names = new string[count];
        for (var i = 0; i < count; i++)
            names[i] = Marshal.PtrToStringUTF8((nint)extensions[i])!;
        return names;
    }

    internal static SurfaceKHR CreateVulkanSurface(SDL_Window* window)
    {
        VulkanContext.EnsureRenderThread();
        var instance = (VkInstance_T*)(void*)VulkanContext.VkInstance.Handle;
        VkSurfaceKHR_T* surface;
        if (!SDL3.SDL_Vulkan_CreateSurface(window, instance, null, &surface))
            throw CreateSdlException("SDL Vulkan surface creation");

        return new SurfaceKHR { Handle = (ulong)surface };
    }

    internal bool PumpEvents()
    {
        VulkanContext.EnsureRenderThread();
        SDL_Event @event = default;
        while (SDL3.SDL_PollEvent(&@event))
        {
            if (@event.Type == SDL_EventType.SDL_EVENT_QUIT)
            {
                _quitRequested = true;
                continue;
            }

            if (@event.Type is SDL_EventType.SDL_EVENT_DROP_FILE or
                SDL_EventType.SDL_EVENT_DROP_TEXT or
                SDL_EventType.SDL_EVENT_DROP_BEGIN or
                SDL_EventType.SDL_EVENT_DROP_COMPLETE or
                SDL_EventType.SDL_EVENT_DROP_POSITION)
            {
                var path = @event.drop.data is null
                    ? null
                    : Marshal.PtrToStringUTF8((nint)@event.drop.data);

                if (_windows.TryGetValue(@event.drop.windowID, out var dropWindow))
                    dropWindow.HandleDropEvent(@event.Type, path);
                continue;
            }

            if (TryGetWindowId(@event, out var windowId) &&
                _windows.TryGetValue(windowId, out var window))
            {
                window.HandleEvent(in @event);
            }
        }

        return _quitRequested;
    }

    internal SDL_Cursor* GetCursor(CursorShape shape)
    {
        VulkanContext.EnsureRenderThread();
        if (_cursors.TryGetValue(shape, out var cursorHandle))
            return (SDL_Cursor*)cursorHandle;

        var systemCursor = shape switch
        {
            CursorShape.Arrow => SDL_SystemCursor.SDL_SYSTEM_CURSOR_DEFAULT,
            CursorShape.IBeam => SDL_SystemCursor.SDL_SYSTEM_CURSOR_TEXT,
            CursorShape.Crosshair => SDL_SystemCursor.SDL_SYSTEM_CURSOR_CROSSHAIR,
            CursorShape.Hand => SDL_SystemCursor.SDL_SYSTEM_CURSOR_POINTER,
            CursorShape.HResize => SDL_SystemCursor.SDL_SYSTEM_CURSOR_EW_RESIZE,
            CursorShape.VResize => SDL_SystemCursor.SDL_SYSTEM_CURSOR_NS_RESIZE,
            CursorShape.NwseResize => SDL_SystemCursor.SDL_SYSTEM_CURSOR_NWSE_RESIZE,
            CursorShape.NeswResize => SDL_SystemCursor.SDL_SYSTEM_CURSOR_NESW_RESIZE,
            CursorShape.ResizeAll => SDL_SystemCursor.SDL_SYSTEM_CURSOR_MOVE,
            CursorShape.NotAllowed => SDL_SystemCursor.SDL_SYSTEM_CURSOR_NOT_ALLOWED,
            CursorShape.Wait => SDL_SystemCursor.SDL_SYSTEM_CURSOR_WAIT,
            CursorShape.WaitArrow => SDL_SystemCursor.SDL_SYSTEM_CURSOR_PROGRESS,
            _ => SDL_SystemCursor.SDL_SYSTEM_CURSOR_DEFAULT
        };

        var cursor = SDL3.SDL_CreateSystemCursor(systemCursor);
        if (cursor is null)
            throw CreateSdlException("SDL system cursor creation");

        _cursors.Add(shape, (nint)cursor);
        return cursor;
    }

    internal void Destroy()
    {
        VulkanContext.EnsureRenderThread();
        foreach (var cursor in _cursors.Values)
        {
            if (cursor != 0)
                SDL3.SDL_DestroyCursor((SDL_Cursor*)cursor);
        }
        _cursors.Clear();

        foreach (var nativeWindow in _nativeWindows.Values)
        {
            if (nativeWindow != 0)
                SDL3.SDL_DestroyWindow((SDL_Window*)nativeWindow);
        }
        _nativeWindows.Clear();
        _windows.Clear();
        _quitRequested = false;

        if (_initialized)
        {
            SDL3.SDL_Vulkan_UnloadLibrary();
            SDL3.SDL_QuitSubSystem(SDL_InitFlags.SDL_INIT_VIDEO);
            _initialized = false;
        }
    }

    internal static void SetWindowPosition(SDL_Window* window, Silk.NET.Maths.Vector2D<int> position)
    {
        VulkanContext.EnsureRenderThread();
        if (!SDL3.SDL_SetWindowPosition(window, position.X, position.Y))
            throw CreateSdlException("SDL window position update");
    }

    internal static void SyncWindow(SDL_Window* window)
    {
        VulkanContext.EnsureRenderThread();
        SDL3.SDL_SyncWindow(window);
    }

    internal static void SetFullscreenVideoMode(SDL_Window* window, VideoMode mode)
    {
        VulkanContext.EnsureRenderThread();
        var displayId = SDL3.SDL_GetDisplayForWindow(window);
        if (displayId == default)
            return;

        var resolution = mode.Resolution;
        if (resolution is null)
        {
            var desktopMode = SDL3.SDL_GetDesktopDisplayMode(displayId);
            if (desktopMode is null)
                throw CreateSdlException("SDL desktop video mode query");
            resolution = new Silk.NET.Maths.Vector2D<int>(desktopMode->w, desktopMode->h);
        }

        var refreshRate = mode.RefreshRate ?? 0;
        SDL_DisplayMode closest = default;
        if (!SDL3.SDL_GetClosestFullscreenDisplayMode(displayId, resolution.Value.X, resolution.Value.Y, refreshRate, true,
                &closest))
            throw CreateSdlException("SDL fullscreen video mode query");
        if (!SDL3.SDL_SetWindowFullscreenMode(window, &closest))
            throw CreateSdlException("SDL fullscreen video mode update");
    }

    private static bool TryGetWindowId(in SDL_Event @event, out SDL_WindowID id)
    {
        id = @event.Type switch
        {
            SDL_EventType.SDL_EVENT_WINDOW_SHOWN or
            SDL_EventType.SDL_EVENT_WINDOW_HIDDEN or
            SDL_EventType.SDL_EVENT_WINDOW_EXPOSED or
            SDL_EventType.SDL_EVENT_WINDOW_MOVED or
            SDL_EventType.SDL_EVENT_WINDOW_RESIZED or
            SDL_EventType.SDL_EVENT_WINDOW_PIXEL_SIZE_CHANGED or
            SDL_EventType.SDL_EVENT_WINDOW_METAL_VIEW_RESIZED or
            SDL_EventType.SDL_EVENT_WINDOW_MINIMIZED or
            SDL_EventType.SDL_EVENT_WINDOW_MAXIMIZED or
            SDL_EventType.SDL_EVENT_WINDOW_RESTORED or
            SDL_EventType.SDL_EVENT_WINDOW_MOUSE_ENTER or
            SDL_EventType.SDL_EVENT_WINDOW_MOUSE_LEAVE or
            SDL_EventType.SDL_EVENT_WINDOW_FOCUS_GAINED or
            SDL_EventType.SDL_EVENT_WINDOW_FOCUS_LOST or
            SDL_EventType.SDL_EVENT_WINDOW_CLOSE_REQUESTED or
            SDL_EventType.SDL_EVENT_WINDOW_HIT_TEST or
            SDL_EventType.SDL_EVENT_WINDOW_ICCPROF_CHANGED or
            SDL_EventType.SDL_EVENT_WINDOW_DISPLAY_CHANGED or
            SDL_EventType.SDL_EVENT_WINDOW_DISPLAY_SCALE_CHANGED or
            SDL_EventType.SDL_EVENT_WINDOW_SAFE_AREA_CHANGED or
            SDL_EventType.SDL_EVENT_WINDOW_OCCLUDED or
            SDL_EventType.SDL_EVENT_WINDOW_ENTER_FULLSCREEN or
            SDL_EventType.SDL_EVENT_WINDOW_LEAVE_FULLSCREEN or
            SDL_EventType.SDL_EVENT_WINDOW_DESTROYED or
            SDL_EventType.SDL_EVENT_WINDOW_HDR_STATE_CHANGED or
            SDL_EventType.SDL_EVENT_WINDOW_SETTINGS_CHANGED => @event.window.windowID,
            SDL_EventType.SDL_EVENT_KEY_DOWN or
            SDL_EventType.SDL_EVENT_KEY_UP => @event.key.windowID,
            SDL_EventType.SDL_EVENT_TEXT_EDITING or
            SDL_EventType.SDL_EVENT_TEXT_INPUT or
            SDL_EventType.SDL_EVENT_TEXT_EDITING_CANDIDATES => @event.text.windowID,
            SDL_EventType.SDL_EVENT_MOUSE_MOTION => @event.motion.windowID,
            SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN or
            SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP => @event.button.windowID,
            SDL_EventType.SDL_EVENT_MOUSE_WHEEL => @event.wheel.windowID,
            _ => default
        };

        return id != default;
    }

    private static InvalidOperationException CreateSdlException(string operation) =>
        new($"{operation} failed: {SDL3.SDL_GetError()}");
}

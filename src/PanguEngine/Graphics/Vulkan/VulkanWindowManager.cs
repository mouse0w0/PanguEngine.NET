using System.Collections.Concurrent;
using PanguEngine.Windowing;
using SDL;
using Silk.NET.Vulkan;

namespace PanguEngine.Graphics.Vulkan;

internal sealed unsafe class VulkanWindowManager : WindowManager
{
    private readonly SdlPlatform _platform;
    private readonly List<WeakReference<VulkanWindow>> _lifetimeWindows = [];
    private readonly ConcurrentQueue<VulkanWindow> _finalizedWindows = new();
    private int _acceptingFinalizers = 1;
    private int _inFlightFinalizerEnqueues;

    internal VulkanWindowManager(SdlPlatform platform)
    {
        _platform = platform;
    }

    internal VulkanWindow CreatePrimaryWindow(
        SDL_Window* nativeWindow,
        SurfaceKHR surface,
        WindowOptions options)
    {
        var window = CreateVulkanWindow(nativeWindow, surface, true, options);
        try
        {
            AddWindow(window);
            return window;
        }
        catch
        {
            DestroyWindow(window);
            throw;
        }
    }

    protected override Window CreateWindowCore(WindowOptions options)
    {
        var nativeWindow = _platform.CreateWindow(options);
        var constructionStarted = false;

        try
        {
            var surface = SdlPlatform.CreateVulkanSurface(nativeWindow);
            constructionStarted = true;
            return CreateVulkanWindow(nativeWindow, surface, false, options);
        }
        catch
        {
            if (!constructionStarted)
                _platform.DestroyWindow(nativeWindow);

            throw;
        }
    }

    protected override void PumpEventsCore()
    {
        if (_platform.PumpEvents())
            HideAll();
    }

    protected override void DestroyCore()
    {
        VulkanContext.EnsureRenderThread();
        var windows = GetLifetimeWindowsSnapshot();
        Interlocked.Exchange(ref _acceptingFinalizers, 0);
        WaitForFinalizerEnqueues();
        DrainFinalized();

        foreach (var window in windows)
            DestroyWindow(window);

        _lifetimeWindows.Clear();
        _finalizedWindows.Clear();
    }

    internal void EnqueueFinalized(VulkanWindow window)
    {
        Interlocked.Increment(ref _inFlightFinalizerEnqueues);
        try
        {
            if (Volatile.Read(ref _acceptingFinalizers) != 0)
                _finalizedWindows.Enqueue(window);
        }
        finally
        {
            Interlocked.Decrement(ref _inFlightFinalizerEnqueues);
        }
    }

    internal void DrainFinalized()
    {
        VulkanContext.EnsureRenderThread();
        while (_finalizedWindows.TryDequeue(out var window))
            DestroyWindow(window);
    }

    private VulkanWindow CreateVulkanWindow(
        SDL_Window* nativeWindow,
        SurfaceKHR surface,
        bool isPrimary,
        WindowOptions options)
    {
        VulkanWindow? window = null;
        try
        {
            window = new VulkanWindow(this, _platform, nativeWindow, surface, isPrimary, options);
            if (options.Icons.Length > 0)
                window.SetWindowIcons(options.Icons);
            _lifetimeWindows.Add(new WeakReference<VulkanWindow>(window, trackResurrection: true));
            return window;
        }
        catch
        {
            window?.Destroy();
            throw;
        }
    }

    private void DestroyWindow(VulkanWindow window)
    {
        if (!window.IsDestroyed)
            window.Destroy();

        for (var i = _lifetimeWindows.Count - 1; i >= 0; i--)
        {
            if (!_lifetimeWindows[i].TryGetTarget(out var candidate) || ReferenceEquals(candidate, window))
                _lifetimeWindows.RemoveAt(i);
        }
    }

    private List<VulkanWindow> GetLifetimeWindowsSnapshot()
    {
        var windows = new List<VulkanWindow>(_lifetimeWindows.Count);
        foreach (var reference in _lifetimeWindows)
        {
            if (reference.TryGetTarget(out var window) && !window.IsDestroyed)
                windows.Add(window);
        }

        return windows;
    }

    private void WaitForFinalizerEnqueues()
    {
        var spinWait = new SpinWait();
        while (Volatile.Read(ref _inFlightFinalizerEnqueues) != 0)
            spinWait.SpinOnce();
    }
}

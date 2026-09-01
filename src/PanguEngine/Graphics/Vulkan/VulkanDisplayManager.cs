using System.Runtime.InteropServices;
using PanguEngine.Windowing;
using SDL;
using Silk.NET.Maths;
using SdlDisplayMode = SDL.SDL_DisplayMode;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Vulkan implementation of <see cref="DisplayManager"/>.
/// </summary>
internal sealed unsafe class VulkanDisplayManager : DisplayManager
{
    /// <summary>Creates a new SDL-backed Vulkan display manager.</summary>
    internal VulkanDisplayManager()
    {
        VulkanContext.EnsureRenderThread();
    }

    /// <inheritdoc/>
    public override IReadOnlyList<DisplayMonitor> Monitors
    {
        get
        {
            VulkanContext.EnsureRenderThread();
            var displays = GetDisplays();
            var result = new DisplayMonitor[displays.Length];
            for (var i = 0; i < displays.Length; i++)
                result[i] = CreateDisplayMonitor(displays[i], i);
            return result;
        }
    }

    /// <inheritdoc/>
    public override DisplayMonitor? MainMonitor
    {
        get
        {
            VulkanContext.EnsureRenderThread();
            return FromSdlDisplay(SDL3.SDL_GetPrimaryDisplay());
        }
    }

    internal static DisplayMonitor? FromSdlDisplay(SDL_DisplayID displayId)
    {
        VulkanContext.EnsureRenderThread();
        if (displayId == default)
            return null;

        var displays = GetDisplays();
        for (var i = 0; i < displays.Length; i++)
        {
            if (displays[i] == displayId)
                return CreateDisplayMonitor(displayId, i);
        }

        return null;
    }

    internal static Rectangle<int>? MainUsableBounds()
    {
        VulkanContext.EnsureRenderThread();
        var displayId = SDL3.SDL_GetPrimaryDisplay();
        if (displayId == default)
            return null;

        SDL_Rect rect = default;
        if (!SDL3.SDL_GetDisplayUsableBounds(displayId, &rect))
            return null;
        return new Rectangle<int>(rect.x, rect.y, rect.w, rect.h);
    }

    private static DisplayMonitor CreateDisplayMonitor(SDL_DisplayID displayId, int index)
    {
        var name = Marshal.PtrToStringUTF8((nint)SDL3.Unsafe_SDL_GetDisplayName(displayId)) ?? "";
        SDL_Rect bounds = default;
        if (!SDL3.SDL_GetDisplayBounds(displayId, &bounds))
            bounds = default;

        var currentMode = SDL3.SDL_GetCurrentDisplayMode(displayId);
        var videoMode = currentMode is null ? VideoMode.Default : FromSdlVideoMode(currentMode);
        var contentScale = SDL3.SDL_GetDisplayContentScale(displayId);
        if (contentScale <= 0)
            contentScale = 1;

        return new DisplayMonitor(
            name,
            index,
            new Rectangle<int>(bounds.x, bounds.y, bounds.w, bounds.h),
            videoMode,
            contentScale,
            GetFullscreenVideoModes(displayId));
    }

    private static VideoMode[] GetFullscreenVideoModes(SDL_DisplayID displayId)
    {
        var count = 0;
        var modes = SDL3.SDL_GetFullscreenDisplayModes(displayId, &count);
        if (modes is null)
            return [];

        try
        {
            var result = new VideoMode[count];
            for (var i = 0; i < count; i++)
                result[i] = FromSdlVideoMode(modes[i]);
            return result;
        }
        finally
        {
            SDL3.SDL_free((nint)modes);
        }
    }

    private static SDL_DisplayID[] GetDisplays()
    {
        var count = 0;
        var displays = SDL3.SDL_GetDisplays(&count);
        if (displays is null)
            return [];

        try
        {
            var result = new SDL_DisplayID[count];
            for (var i = 0; i < count; i++)
                result[i] = displays[i];
            return result;
        }
        finally
        {
            SDL3.SDL_free((nint)displays);
        }
    }

    internal static VideoMode FromSdlVideoMode(SdlDisplayMode* mode)
    {
        var refreshRate = mode->refresh_rate <= 0 ? null : (int?)Math.Round(mode->refresh_rate);
        return new VideoMode(new Vector2D<int>(mode->w, mode->h), refreshRate);
    }
}

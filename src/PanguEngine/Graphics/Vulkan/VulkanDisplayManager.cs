using PanguEngine.Windowing;
using Silk.NET.GLFW;
using SilkMonitor = Silk.NET.Windowing.Monitor;
using ISilkMonitor = Silk.NET.Windowing.IMonitor;
using SilkVideoMode = Silk.NET.Windowing.VideoMode;
using SilkWindow = Silk.NET.Windowing.IWindow;
using EngineVideoMode = PanguEngine.Windowing.VideoMode;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Vulkan implementation of <see cref="DisplayManager"/>.
/// </summary>
internal sealed unsafe class VulkanDisplayManager : DisplayManager
{
    private readonly SilkWindow _window;

    /// <summary>Creates a new Vulkan display manager.</summary>
    /// <param name="window">The window used to query platform display state.</param>
    internal VulkanDisplayManager(SilkWindow window)
    {
        _window = window;
    }

    /// <inheritdoc/>
    public override IReadOnlyList<DisplayMonitor> Monitors
    {
        get
        {
            var result = new List<DisplayMonitor>();
            foreach (var monitor in SilkMonitor.GetMonitors(_window))
                result.Add(CreateDisplayMonitor(monitor));

            return result;
        }
    }

    /// <inheritdoc/>
    public override DisplayMonitor? MainMonitor =>
        FromSilkMonitor(SilkMonitor.GetMainMonitor(_window));

    /// <summary>Converts an engine video mode to a Silk.NET video mode.</summary>
    /// <param name="mode">The engine video mode.</param>
    /// <returns>The Silk.NET video mode.</returns>
    internal static SilkVideoMode ToSilkVideoMode(EngineVideoMode mode)
    {
        return new SilkVideoMode(mode.Resolution, mode.RefreshRate);
    }

    /// <summary>Converts a Silk.NET video mode to an engine video mode.</summary>
    /// <param name="mode">The Silk.NET video mode.</param>
    /// <returns>The engine video mode.</returns>
    internal static EngineVideoMode FromSilkVideoMode(SilkVideoMode mode)
    {
        return new EngineVideoMode(mode.Resolution, mode.RefreshRate);
    }

    /// <summary>Creates an engine display monitor from a Silk.NET monitor.</summary>
    /// <param name="monitor">The Silk.NET monitor, or <see langword="null" /> when unavailable.</param>
    /// <returns>The engine display monitor, or <see langword="null" /> when unavailable.</returns>
    internal static DisplayMonitor? FromSilkMonitor(ISilkMonitor? monitor)
    {
        if (monitor is null)
            return null;

        return CreateDisplayMonitor(monitor);
    }

    /// <summary>Creates an engine display monitor snapshot.</summary>
    /// <param name="monitor">The Silk.NET monitor to describe.</param>
    /// <returns>The engine display monitor snapshot.</returns>
    internal static DisplayMonitor CreateDisplayMonitor(ISilkMonitor monitor)
    {
        var glfw = GlfwProvider.GLFW.Value;
        var monitors = glfw.GetMonitors(out _);
        glfw.GetMonitorContentScale(monitors[monitor.Index], out var xScale, out _);

        var modes = new List<EngineVideoMode>();
        foreach (var mode in monitor.GetAllVideoModes())
            modes.Add(FromSilkVideoMode(mode));

        return new DisplayMonitor(
            monitor.Name,
            monitor.Index,
            monitor.Bounds,
            FromSilkVideoMode(monitor.VideoMode),
            monitor.Gamma,
            xScale,
            modes.ToArray());
    }
}

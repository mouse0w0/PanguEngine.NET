using System.Globalization;
using PanguEngine.Client;
using Silk.NET.Maths;

namespace PanguEngine;

/// <summary>
/// Starts the engine from command-line arguments.
/// </summary>
public static class Bootstrap
{
    /// <summary>
    /// Launches the client application.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    public static void Launch(string[] args)
    {
        var options = ParseOptions(args);
        ClientEngine.Start(options);
    }

    /// <summary>
    /// Parses command-line arguments into launch options.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>The parsed launch options.</returns>
    internal static LaunchOptions ParseOptions(string[] args)
    {
        var modPaths = new List<string>();
        var gpuValidation = false;
        string? windowTitle = null;
        Vector2D<int>? windowSize = null;
        WindowMode? windowMode = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--mod":
                    if (i + 1 >= args.Length)
                        throw new ArgumentException("--mod requires a path.");

                    var path = args[++i];
                    if (string.IsNullOrWhiteSpace(path))
                        throw new ArgumentException("--mod path cannot be empty.");

                    modPaths.Add(path);
                    break;
                case "--gpu-validation":
                    gpuValidation = true;
                    break;
                case "--window-title":
                    if (i + 1 >= args.Length)
                        throw new ArgumentException("--window-title requires a title.");
                    if (windowTitle is not null)
                        throw new ArgumentException("--window-title cannot be specified more than once.");

                    var title = args[++i];
                    if (string.IsNullOrWhiteSpace(title))
                        throw new ArgumentException("--window-title cannot be empty.");

                    windowTitle = title;
                    break;
                case "--window-size":
                    if (i + 1 >= args.Length)
                        throw new ArgumentException("--window-size requires a size.");
                    if (windowSize.HasValue)
                        throw new ArgumentException("--window-size cannot be specified more than once.");

                    var size = args[++i];
                    if (!TryParseWindowSize(size, out var parsedSize))
                        throw new ArgumentException("--window-size must use a positive WIDTHxHEIGHT value.");

                    windowSize = parsedSize;
                    break;
                case "--window-mode":
                    if (i + 1 >= args.Length)
                        throw new ArgumentException("--window-mode requires a mode.");
                    if (windowMode.HasValue)
                        throw new ArgumentException("--window-mode cannot be specified more than once.");

                    var mode = args[++i].ToLowerInvariant();
                    windowMode = mode switch
                    {
                        "windowed" => WindowMode.Windowed,
                        "maximized" => WindowMode.Maximized,
                        "fullscreen" => WindowMode.Fullscreen,
                        "borderless" => WindowMode.Borderless,
                        _ => throw new ArgumentException($"Unknown window mode '{args[i]}'.")
                    };
                    break;
                default:
                    throw new ArgumentException($"Unknown argument '{args[i]}'.");
            }
        }

        return new LaunchOptions
        {
            GpuValidation = gpuValidation,
            ModPaths = modPaths.ToArray(),
            WindowTitle = windowTitle,
            WindowSize = windowSize,
            WindowMode = windowMode
        };
    }

    private static bool TryParseWindowSize(string value, out Vector2D<int> size)
    {
        size = default;
        var separator = value.IndexOf('x');
        if (separator <= 0 || separator == value.Length - 1)
            return false;

        if (!int.TryParse(value[..separator], NumberStyles.None, CultureInfo.InvariantCulture, out var width) ||
            !int.TryParse(value[(separator + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var height) ||
            width <= 0 || height <= 0)
            return false;

        size = new Vector2D<int>(width, height);
        return true;
    }
}
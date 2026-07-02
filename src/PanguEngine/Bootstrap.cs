using PanguEngine.Client;

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
        new ClientEngine(options).Run();
    }

    /// <summary>
    /// Parses command-line arguments into launch options.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>The parsed launch options.</returns>
    internal static LaunchOptions ParseOptions(string[] args)
    {
        var modPaths = new List<string>();

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
                default:
                    throw new ArgumentException($"Unknown argument '{args[i]}'.");
            }
        }

        return new LaunchOptions { ModPaths = modPaths.ToArray() };
    }
}
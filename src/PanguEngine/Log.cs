using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NLog;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Targets;
using NLog.Targets.Wrappers;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using LogLevel = NLog.LogLevel;

namespace PanguEngine;

/// <summary>
/// Static logging facade.
/// </summary>
public static class Log
{
    /// <summary>The logger factory for creating loggers.</summary>
    public static ILoggerFactory Factory { get; private set; } = NullLoggerFactory.Instance;

    /// <summary>Get a logger for the specified type.</summary>
    public static ILogger<T> CreateLogger<T>() => Factory.CreateLogger<T>();

    /// <summary>Get a logger for the specified category name.</summary>
    public static ILogger CreateLogger(string categoryName) => Factory.CreateLogger(categoryName);

    /// <summary>
    /// Initialize the logging subsystem.
    /// </summary>
    internal static void Initialize()
    {
        var config = new LoggingConfiguration();

        var layout =
            "${date} | ${level:uppercase=true:padding=-5} | ${logger} | ${message}${onexception:inner=\n${exception}}";

        var consoleTarget = new ColoredConsoleTarget("console")
        {
            Layout = layout,
            DetectConsoleAvailable = true
        };
        var asyncConsoleTarget = new AsyncTargetWrapper(consoleTarget)
        {
            OverflowAction = AsyncTargetWrapperOverflowAction.Block
        };
        config.AddTarget(asyncConsoleTarget);

        var fileTarget = new FileTarget("file")
        {
            FileName = "${basedir}/logs/latest.log",
            Layout = layout,
            KeepFileOpen = true,
            MaxArchiveFiles = 30,
            ArchiveOldFileOnStartup = true,
            ArchiveEvery = FileArchivePeriod.Day,
            ArchiveFileName = "${basedir}/logs/${shortdate}.log",
            ArchiveSuffixFormat = "-{0}"
        };
        var asyncFileTarget = new AsyncTargetWrapper(fileTarget)
        {
            OverflowAction = AsyncTargetWrapperOverflowAction.Block
        };
        config.AddTarget(asyncFileTarget);

        config.AddRule(LogLevel.Debug, LogLevel.Fatal, asyncConsoleTarget);
        config.AddRule(LogLevel.Debug, LogLevel.Fatal, asyncFileTarget);

        LogManager.Configuration = config;

        Factory = LoggerFactory.Create(builder => builder.AddNLog());
    }

    /// <summary>Flush and close the logging subsystem.</summary>
    internal static void Shutdown()
    {
        Factory.Dispose();
        Factory = NullLoggerFactory.Instance;
        LogManager.Shutdown();
    }
}
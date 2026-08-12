using Microsoft.Extensions.Logging;
using PanguEngine.Audio.OpenAL;

namespace PanguEngine.Audio.Backend;

internal static class AudioBackendFactory
{
    private static readonly Action<ILogger, Exception?> LogBackendUnavailable = LoggerMessage.Define(
        LogLevel.Warning,
        new EventId(1, nameof(LogBackendUnavailable)),
        "Audio output is unavailable; using the null audio backend");

    internal static IAudioBackend Create(ILogger logger)
    {
        try
        {
            return new OpenAlAudioBackend(logger);
        }
        catch (AudioBackendInitializationException exception)
        {
            LogBackendUnavailable(logger, exception);
            return new NullAudioBackend();
        }
    }
}

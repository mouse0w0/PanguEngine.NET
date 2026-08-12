namespace PanguEngine.Audio.Backend;

internal class AudioBackendException(string message, Exception? innerException = null)
    : Exception(message, innerException);

internal sealed class AudioBackendInitializationException(string message, Exception? innerException = null)
    : AudioBackendException(message, innerException);

namespace PanguEngine.Network;

internal sealed class ConnectionClosedException(string message) : Exception(message);

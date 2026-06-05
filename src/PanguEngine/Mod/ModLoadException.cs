namespace PanguEngine.Mod;

public sealed class ModLoadException : Exception
{
    public ModLoadException(string message) : base(message)
    {
    }

    public ModLoadException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
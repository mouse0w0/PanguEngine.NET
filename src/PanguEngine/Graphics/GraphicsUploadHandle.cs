namespace PanguEngine.Graphics;

public abstract class GraphicsUploadHandle
{
    public abstract bool IsCompleted { get; }

    public abstract bool IsFaulted { get; }

    public abstract Exception? Exception { get; }

    public abstract void Wait();
}
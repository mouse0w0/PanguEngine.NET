namespace PanguEngine.Mod;

internal abstract class ModSource(string sourcePath) : IDisposable
{
    protected string SourcePath { get; } = sourcePath;

    public abstract bool Exists(string path);

    public abstract Stream Open(string path);

    public abstract IEnumerable<string> GetAssemblyFileNames();

    public virtual bool TryGetFilePath(string path, out string? filePath)
    {
        filePath = null;
        return false;
    }

    public virtual void Dispose()
    {
    }
}
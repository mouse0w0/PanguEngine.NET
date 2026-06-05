namespace PanguEngine.Mod;

internal abstract class ModSource(string sourcePath) : IDisposable
{
    protected string SourcePath { get; } = sourcePath;

    public abstract bool Exists(string path);

    public abstract Stream Open(string path);

    public abstract IEnumerable<string> GetAssemblyFileNames();

    public virtual void Dispose()
    {
    }
}
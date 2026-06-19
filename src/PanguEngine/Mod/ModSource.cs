using System.Diagnostics.CodeAnalysis;

namespace PanguEngine.Mod;

/// <summary>
/// Provides file access for a mod source.
/// </summary>
/// <param name="sourcePath">The source path for the mod.</param>
internal abstract class ModSource(string sourcePath) : IDisposable
{
    /// <summary>
    /// Gets the source path for the mod.
    /// </summary>
    public string SourcePath { get; } = sourcePath;

    /// <summary>
    /// Determines whether a file exists in the mod source.
    /// </summary>
    /// <param name="path">The file path in the mod source.</param>
    /// <returns><see langword="true" /> if the file exists; otherwise, <see langword="false" />.</returns>
    public abstract bool Exists(string path);

    /// <summary>
    /// Opens a file from the mod source.
    /// </summary>
    /// <param name="path">The file path in the mod source.</param>
    /// <returns>A readable stream for the file.</returns>
    public abstract Stream Open(string path);

    /// <summary>
    /// Gets the assembly file names available in the mod source.
    /// </summary>
    /// <returns>The assembly file names.</returns>
    public abstract IEnumerable<string> GetAssemblyFileNames();

    /// <summary>
    /// Attempts to resolve a source file to a local file path.
    /// </summary>
    /// <param name="path">The file path in the mod source.</param>
    /// <param name="filePath">The resolved local file path.</param>
    /// <returns><see langword="true" /> if a local file path is available; otherwise, <see langword="false" />.</returns>
    public virtual bool TryGetFilePath(string path, [NotNullWhen(true)] out string? filePath)
    {
        filePath = null;
        return false;
    }

    /// <summary>
    /// Releases resources held by the mod source.
    /// </summary>
    public virtual void Dispose()
    {
    }
}
using System.Text;

namespace PanguEngine.Resources;

/// <summary>
/// Represents a file resource resolved from a specific resource source.
/// </summary>
public sealed class Resource
{
    private readonly Func<Stream> _open;

    /// <summary>
    /// Creates a resource handle that opens through its source.
    /// </summary>
    /// <param name="path">The resource path.</param>
    /// <param name="source">The source that provides the resource.</param>
    internal Resource(string path, IResourceSource source)
        : this(path, source, () => source.Open(path))
    {
    }

    /// <summary>
    /// Creates a resource handle with a bound open operation.
    /// </summary>
    /// <param name="path">The resource path.</param>
    /// <param name="source">The source that provides the resource.</param>
    /// <param name="open">The operation used to open the resource.</param>
    internal Resource(string path, IResourceSource source, Func<Stream> open)
    {
        Path = path;
        Source = source;
        _open = open;
    }

    /// <summary>
    /// Gets the normalized path of the resource within the resource root.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the source that provides this resource.
    /// </summary>
    public IResourceSource Source { get; }

    /// <summary>
    /// Opens a readable stream for the resource.
    /// </summary>
    /// <returns>A new stream for reading the resource content.</returns>
    public Stream Open()
    {
        return _open();
    }

    /// <summary>
    /// Reads the resource content as bytes.
    /// </summary>
    /// <returns>The full resource content.</returns>
    public byte[] ReadAllBytes()
    {
        using var stream = Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    /// <summary>
    /// Reads the resource content as UTF-8 text.
    /// </summary>
    /// <returns>The full resource content.</returns>
    public string ReadAllText()
    {
        using var stream = Open();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
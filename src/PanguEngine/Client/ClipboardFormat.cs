namespace PanguEngine.Client;

/// <summary>
/// Identifies a clipboard data format by its MIME type.
/// </summary>
public sealed record ClipboardFormat
{
    /// <summary>
    /// Initializes a clipboard format.
    /// </summary>
    /// <param name="mimeType">The MIME type that identifies the format.</param>
    public ClipboardFormat(string mimeType)
    {
        ArgumentException.ThrowIfNullOrEmpty(mimeType);
        MimeType = mimeType;
    }

    /// <summary>
    /// Gets the UTF-8 plain text format.
    /// </summary>
    public static ClipboardFormat Text { get; } = new("text/plain;charset=utf-8");

    /// <summary>
    /// Gets the HTML format.
    /// </summary>
    public static ClipboardFormat Html { get; } = new("text/html");

    /// <summary>
    /// Gets the rich text format.
    /// </summary>
    public static ClipboardFormat Rtf { get; } = new("text/rtf");

    /// <summary>
    /// Gets the URI list format.
    /// </summary>
    public static ClipboardFormat UriList { get; } = new("text/uri-list");

    /// <summary>
    /// Gets the bitmap image format.
    /// </summary>
    public static ClipboardFormat Bmp { get; } = new("image/bmp");

    /// <summary>
    /// Gets the PNG image format.
    /// </summary>
    public static ClipboardFormat Png { get; } = new("image/png");

    /// <summary>
    /// Gets the JPEG image format.
    /// </summary>
    public static ClipboardFormat Jpeg { get; } = new("image/jpeg");

    /// <summary>
    /// Gets the MIME type that identifies the format.
    /// </summary>
    public string MimeType { get; }

    /// <inheritdoc />
    public override string ToString() => MimeType;
}

namespace PanguEngine.Client;

/// <summary>
/// Provides synchronous access to the system clipboard while client services are active.
/// </summary>
/// <remarks>All members must be called from the SDL main thread.</remarks>
public abstract class Clipboard
{
    private bool _destroyed;

    internal Clipboard()
    {
    }

    /// <summary>
    /// Gets a snapshot of the formats currently present on the system clipboard.
    /// </summary>
    public IReadOnlyList<ClipboardFormat> Formats
    {
        get
        {
            EnsureNotDestroyed();
            return GetFormatsCore();
        }
    }

    /// <summary>
    /// Gets whether the system clipboard contains non-empty plain text.
    /// </summary>
    public bool HasText
    {
        get
        {
            EnsureNotDestroyed();
            return TryGetTextCore(out var text) && text.Length != 0;
        }
    }

    /// <summary>
    /// Determines whether the system clipboard contains an exact data format.
    /// </summary>
    /// <param name="format">The format to find.</param>
    /// <returns><see langword="true"/> when the format is present.</returns>
    public bool Contains(ClipboardFormat format)
    {
        EnsureNotDestroyed();
        ArgumentNullException.ThrowIfNull(format);
        return GetFormatsCore().Contains(format);
    }

    /// <summary>
    /// Tries to read plain text from the system clipboard.
    /// </summary>
    /// <param name="text">The text, or an empty string when no text is present.</param>
    /// <returns><see langword="true"/> when text was read.</returns>
    public bool TryGetText(out string text)
    {
        EnsureNotDestroyed();
        if (TryGetTextCore(out text) && text.Length != 0)
            return true;

        text = string.Empty;
        return false;
    }

    /// <summary>
    /// Tries to read a copy of the data for an exact format.
    /// </summary>
    /// <param name="format">The format to read.</param>
    /// <param name="data">The copied data, or an empty array when the format is absent.</param>
    /// <returns><see langword="true"/> when data was read.</returns>
    public bool TryGetData(ClipboardFormat format, out byte[] data)
    {
        EnsureNotDestroyed();
        ArgumentNullException.ThrowIfNull(format);
        if (TryGetDataCore(format, out data))
            return true;

        data = [];
        return false;
    }

    /// <summary>
    /// Replaces the system clipboard with plain text, or clears it when the text is empty.
    /// </summary>
    /// <param name="text">The plain text value.</param>
    public void SetText(string text)
    {
        EnsureNotDestroyed();
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
        {
            ClearCore();
            return;
        }

        SetTextCore(text);
    }

    /// <summary>
    /// Replaces the system clipboard with a snapshot of a multi-format content item, or clears it when empty.
    /// </summary>
    /// <param name="content">The content to publish.</param>
    public void SetContent(ClipboardContent content)
    {
        EnsureNotDestroyed();
        ArgumentNullException.ThrowIfNull(content);
        if (content.Formats.Count == 0)
        {
            ClearCore();
            return;
        }

        SetContentCore(content);
    }

    /// <summary>
    /// Clears the system clipboard.
    /// </summary>
    public void Clear()
    {
        EnsureNotDestroyed();
        ClearCore();
    }

    internal void Destroy()
    {
        if (_destroyed)
            return;

        try
        {
            DestroyCore();
        }
        finally
        {
            _destroyed = true;
        }
    }

    protected abstract IReadOnlyList<ClipboardFormat> GetFormatsCore();

    protected abstract bool TryGetTextCore(out string text);

    protected abstract bool TryGetDataCore(ClipboardFormat format, out byte[] data);

    protected abstract void SetTextCore(string text);

    protected abstract void SetContentCore(ClipboardContent content);

    protected abstract void ClearCore();

    protected abstract void DestroyCore();

    private void EnsureNotDestroyed()
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
    }
}

using System.Text;

namespace PanguEngine.Client;

/// <summary>
/// Represents a set of clipboard values indexed by data format.
/// </summary>
public sealed class ClipboardContent
{
    private readonly List<ClipboardContentEntry> _entries = [];

    /// <summary>
    /// Initializes an empty clipboard content item.
    /// </summary>
    public ClipboardContent()
    {
    }

    /// <summary>
    /// Gets the formats currently present in this content.
    /// </summary>
    public IReadOnlyList<ClipboardFormat> Formats => _entries.Select(static entry => entry.Format).ToArray();

    /// <summary>
    /// Sets the UTF-8 plain text value, or removes it when the value is empty.
    /// </summary>
    /// <param name="text">The plain text value.</param>
    public void SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
        {
            Remove(ClipboardFormat.Text);
            return;
        }

        SetData(ClipboardFormat.Text, Encoding.UTF8.GetBytes(text));
    }

    /// <summary>
    /// Sets the value for a data format by copying the supplied bytes.
    /// </summary>
    /// <param name="format">The data format.</param>
    /// <param name="data">The data bytes to copy.</param>
    public void SetData(ClipboardFormat format, ReadOnlySpan<byte> data)
    {
        ArgumentNullException.ThrowIfNull(format);
        var entry = new ClipboardContentEntry(format, data.ToArray());
        var index = _entries.FindIndex(candidate => candidate.Format == format);
        if (index >= 0)
        {
            _entries[index] = entry;
            return;
        }

        _entries.Add(entry);
    }

    /// <summary>
    /// Removes all values from this content.
    /// </summary>
    public void Clear() => _entries.Clear();

    internal IReadOnlyList<ClipboardContentEntry> CopyEntries()
    {
        return _entries
            .Select(static entry => new ClipboardContentEntry(entry.Format, entry.Data.ToArray()))
            .ToArray();
    }

    private void Remove(ClipboardFormat format)
    {
        var index = _entries.FindIndex(candidate => candidate.Format == format);
        if (index >= 0)
        {
            _entries.RemoveAt(index);
        }
    }
}

internal readonly record struct ClipboardContentEntry(ClipboardFormat Format, byte[] Data);

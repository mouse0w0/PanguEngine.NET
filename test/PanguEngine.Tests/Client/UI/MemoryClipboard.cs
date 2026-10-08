using System.Text;
using PanguEngine.Desktop;

namespace PanguEngine.Tests.Client.UI;

internal sealed class MemoryClipboard : Clipboard
{
    private string _text = string.Empty;

    protected override IReadOnlyList<ClipboardFormat> GetFormatsCore() =>
        _text.Length == 0 ? [] : [ClipboardFormat.Text];

    protected override bool TryGetTextCore(out string text)
    {
        text = _text;
        return text.Length != 0;
    }

    protected override bool TryGetDataCore(ClipboardFormat format, out byte[] data)
    {
        if (format == ClipboardFormat.Text && TryGetTextCore(out var text))
        {
            data = Encoding.UTF8.GetBytes(text);
            return true;
        }

        data = [];
        return false;
    }

    protected override void SetTextCore(string text) => _text = text;

    protected override void SetContentCore(ClipboardContent content)
    {
        _text = string.Empty;
        foreach (var entry in content.CopyEntries())
        {
            if (entry.Format != ClipboardFormat.Text)
                continue;
            _text = Encoding.UTF8.GetString(entry.Data);
            return;
        }
    }

    protected override void ClearCore() => _text = string.Empty;
}

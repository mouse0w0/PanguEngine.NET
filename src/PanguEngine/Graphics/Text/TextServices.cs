namespace PanguEngine.Graphics.Text;

/// <summary>
/// Provides the process text services.
/// </summary>
public static class TextServices
{
    private static FontManager? _fontManager;
    private static TextLayoutEngine? _textLayoutEngine;

    /// <summary>
    /// Gets the process font manager.
    /// </summary>
    public static FontManager FontManager => _fontManager ?? throw new InvalidOperationException(
        "Text services have not been initialized.");

    /// <summary>
    /// Gets the process CPU text layout engine.
    /// </summary>
    public static TextLayoutEngine TextLayoutEngine => _textLayoutEngine ?? throw new InvalidOperationException(
        "Text services have not been initialized.");

    internal static void Initialize()
    {
        if (_fontManager is not null)
            throw new InvalidOperationException("Text services are already initialized.");

        var fontManager = new FontManager();
        try
        {
            var textLayoutEngine = new TextLayoutEngine(fontManager);
            _fontManager = fontManager;
            _textLayoutEngine = textLayoutEngine;
        }
        catch
        {
            fontManager.Destroy();
            throw;
        }
    }

    internal static void Shutdown()
    {
        var fontManager = _fontManager;
        if (fontManager is null)
            return;

        fontManager.Destroy();
        _textLayoutEngine = null;
        _fontManager = null;
    }
}
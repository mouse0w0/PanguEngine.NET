using Microsoft.Extensions.Logging;
using PanguEngine.Desktop;
using PanguEngine.Graphics.Text;

namespace PanguEngine.Client.UI;

/// <summary>
/// Provides the shared UI settings and services for the process.
/// </summary>
/// <remarks>
/// Read and write toolkit state on the host UI thread. Reads from other threads have no synchronization guarantees.
/// Before initialization or after shutdown, the host may configure settings on its intended UI thread.
/// </remarks>
public static class UiToolkit
{
    private static FontManager? _fontManager;
    private static TextLayoutEngine? _textLayoutEngine;
    private static Clipboard? _clipboard;
    private static ILogger? _logger;
    private static int _ownerThreadId;
    private static double _defaultScale = 1;
    private static int _wheelScrollLines = 3;

    /// <summary>
    /// Gets or sets the default scale used by UI roots without a local scale.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is not finite or is not greater than zero.
    /// </exception>
    /// <exception cref="InvalidOperationException">Thrown when modified from another thread while initialized.</exception>
    public static double DefaultScale
    {
        get => _defaultScale;
        set
        {
            VerifyAccess();
            ValidateScale(value, nameof(value));
            _defaultScale = value;
        }
    }

    /// <summary>
    /// Gets or sets the number of small steps per wheel notch. Zero disables default wheel scrolling,
    /// and minus one selects page scrolling.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is less than minus one.</exception>
    /// <exception cref="InvalidOperationException">Thrown when modified from another thread while initialized.</exception>
    public static int WheelScrollLines
    {
        get => _wheelScrollLines;
        set
        {
            VerifyAccess();
            ValidateWheelScrollLines(value, nameof(value));
            _wheelScrollLines = value;
        }
    }

    /// <summary>
    /// Gets the shared font manager supplied by the host.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the toolkit is not initialized.</exception>
    public static FontManager FontManager => _fontManager ??
        throw new InvalidOperationException("UI toolkit services have not been initialized.");

    /// <summary>
    /// Gets the shared text layout engine supplied by the host.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the toolkit is not initialized.</exception>
    public static TextLayoutEngine TextLayoutEngine => _textLayoutEngine ??
        throw new InvalidOperationException("UI toolkit services have not been initialized.");

    /// <summary>
    /// Gets the shared system clipboard supplied by the host.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the toolkit is not initialized.</exception>
    public static Clipboard Clipboard => _clipboard ??
        throw new InvalidOperationException("UI toolkit services have not been initialized.");

    /// <summary>
    /// Gets the shared UI logger supplied by the host.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the toolkit is not initialized.</exception>
    public static ILogger Logger => _logger ??
        throw new InvalidOperationException("UI toolkit services have not been initialized.");

    /// <summary>
    /// Binds the host's services and default settings to the toolkit on the UI thread.
    /// The host retains ownership of the supplied services.
    /// </summary>
    /// <param name="fontManager">The shared font manager.</param>
    /// <param name="textLayoutEngine">The shared text layout engine.</param>
    /// <param name="clipboard">The shared system clipboard.</param>
    /// <param name="logger">The shared UI logger.</param>
    /// <param name="defaultScale">The default scale for UI roots.</param>
    /// <param name="wheelScrollLines">The default wheel line count, or minus one for page scrolling.</param>
    /// <exception cref="ArgumentNullException">Thrown when a supplied service is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a default setting is invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when services are already bound.</exception>
    public static void Initialize(
        FontManager fontManager,
        TextLayoutEngine textLayoutEngine,
        Clipboard clipboard,
        ILogger logger,
        double defaultScale,
        int wheelScrollLines)
    {
        ArgumentNullException.ThrowIfNull(fontManager);
        ArgumentNullException.ThrowIfNull(textLayoutEngine);
        ArgumentNullException.ThrowIfNull(clipboard);
        ArgumentNullException.ThrowIfNull(logger);
        ValidateScale(defaultScale, nameof(defaultScale));
        ValidateWheelScrollLines(wheelScrollLines, nameof(wheelScrollLines));

        if (_fontManager is not null)
            throw new InvalidOperationException("UI toolkit services are already initialized.");

        _defaultScale = defaultScale;
        _wheelScrollLines = wheelScrollLines;
        _fontManager = fontManager;
        _textLayoutEngine = textLayoutEngine;
        _clipboard = clipboard;
        _logger = logger;
        _ownerThreadId = Environment.CurrentManagedThreadId;
    }

    /// <summary>
    /// Releases the host's service bindings on the UI thread without destroying services or resetting settings.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when called from another thread while initialized.</exception>
    public static void Shutdown()
    {
        if (_fontManager is null)
            return;
        VerifyAccess();

        _fontManager = null;
        _textLayoutEngine = null;
        _clipboard = null;
        _logger = null;
        _ownerThreadId = 0;
    }

    /// <summary>
    /// Validates a UI scale value.
    /// </summary>
    internal static void ValidateScale(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName, "UI scale must be finite and greater than zero.");
        }
    }

    /// <summary>
    /// Gets the distance represented by one wheel notch for a scrollable control.
    /// </summary>
    internal static double GetWheelScrollStep(int lines, double smallChange, double pageSize) =>
        lines == -1 ? pageSize : Math.Min(lines * smallChange, pageSize);

    private static void ValidateWheelScrollLines(int value, string parameterName)
    {
        if (value < -1)
            throw new ArgumentOutOfRangeException(parameterName, "Wheel scroll lines must be minus one or non-negative.");
    }

    private static void VerifyAccess()
    {
        if (_ownerThreadId != 0 && _ownerThreadId != Environment.CurrentManagedThreadId)
            throw new InvalidOperationException("UI toolkit changes require its host UI thread.");
    }
}

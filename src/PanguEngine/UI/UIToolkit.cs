using Microsoft.Extensions.Logging;
using PanguEngine.Desktop;
using PanguEngine.Graphics.Text;

namespace PanguEngine.UI;

/// <summary>
/// Provides the shared UI settings, services, and frame scheduling for the process.
/// </summary>
/// <remarks>
/// Read and write toolkit state on the host UI thread. Reads from other threads have no synchronization guarantees.
/// Before initialization or after shutdown, the host may configure settings on its intended UI thread.
/// Initialization establishes one shared UI thread for all open scenes. UI thread diagnostics are enabled only in Debug builds.
/// </remarks>
public static class UIToolkit
{
    private static readonly Lock PostSync = new();
    private static readonly Queue<Action> PendingActions = [];
    private static readonly LinkedList<Ticker> Tickers = new();
    private static bool _acceptingPosts;
    private static bool _isInitialized;
    private static FontManager? _fontManager;
    private static TextLayoutEngine? _textLayoutEngine;
    private static Clipboard? _clipboard;
    private static ILogger? _logger;
#if DEBUG
    private static int _ownerThreadId;
#endif
    private static double _defaultScale = 1;
    private static int _wheelScrollLines = 3;

    internal static bool IsUpdatingFrame { get; private set; }

    /// <summary>
    /// Gets or sets the default scale used by UI roots without a local scale.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is not finite or is not greater than zero.
    /// </exception>
    /// <exception cref="InvalidOperationException">Thrown in Debug builds when modified from another thread while initialized.</exception>
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
    /// <exception cref="InvalidOperationException">Thrown in Debug builds when modified from another thread while initialized.</exception>
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
                                             throw new InvalidOperationException(
                                                 "UI toolkit services have not been initialized.");

    /// <summary>
    /// Gets the shared text layout engine supplied by the host.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the toolkit is not initialized.</exception>
    public static TextLayoutEngine TextLayoutEngine => _textLayoutEngine ??
                                                       throw new InvalidOperationException(
                                                           "UI toolkit services have not been initialized.");

    /// <summary>
    /// Gets the shared system clipboard supplied by the host.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the toolkit is not initialized.</exception>
    public static Clipboard Clipboard => _clipboard ??
                                         throw new InvalidOperationException(
                                             "UI toolkit services have not been initialized.");

    /// <summary>
    /// Gets the shared UI logger supplied by the host.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the toolkit is not initialized.</exception>
    public static ILogger Logger => _logger ??
                                    throw new InvalidOperationException(
                                        "UI toolkit services have not been initialized.");

    /// <summary>Posts an action for a subsequent UI frame without associating it with a scene.</summary>
    /// <param name="action">The action to execute on the host UI thread.</param>
    /// <remarks>
    /// May be called from any thread while the toolkit is initialized. Actions execute in queue order.
    /// Actions posted during a batch wait for the next frame. Closing a scene does not cancel queued actions.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the toolkit is not accepting actions.</exception>
    public static void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (PostSync)
        {
            if (!_acceptingPosts)
                throw new InvalidOperationException("The UI toolkit is not accepting posted actions.");
            PendingActions.Enqueue(action);
        }
    }

    /// <summary>Executes a batch of posted actions and advances registered tickers for one UI frame.</summary>
    /// <param name="frameTime">The absolute monotonic time shared by this frame's ticker callbacks.</param>
    /// <remarks>
    /// The host calls this once per UI frame, before game frame callbacks, scene layout, and drawing.
    /// Ticker registrations are frozen after posted actions. Callback failures propagate without rollback.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the toolkit is not initialized or an update is already in progress.
    /// Also thrown in Debug builds when called from outside the host UI thread.
    /// </exception>
    public static void UpdateFrame(TimeSpan frameTime)
    {
        VerifyInitialized();
        VerifyAccess();
        if (IsUpdatingFrame)
            throw new InvalidOperationException("The UI toolkit is already updating.");

        IsUpdatingFrame = true;
        try
        {
            DrainPending();
            var registrations = FreezeTickers();
            foreach (var registration in registrations)
                registration.Value.Tick(registration, frameTime);
        }
        finally
        {
            IsUpdatingFrame = false;
        }
    }

    /// <summary>Creates an inactive ticker whose lifecycle is controlled by its consumer.</summary>
    /// <param name="onTick">The callback receiving the absolute monotonic frame time.</param>
    /// <returns>A ticker controlled by its consumer and the current toolkit runtime.</returns>
    /// <remarks>
    /// Creation may precede initialization. Starting requires an initialized toolkit.
    /// Consumers must stop runs that are no longer needed. Tickers must not be reused after toolkit shutdown.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="onTick"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown in Debug builds for access outside the initialized UI thread.</exception>
    public static Ticker CreateTicker(Action<TimeSpan> onTick)
    {
        ArgumentNullException.ThrowIfNull(onTick);
        VerifyAccess();
        return new Ticker(onTick);
    }

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

        if (_isInitialized)
            throw new InvalidOperationException("UI toolkit services are already initialized.");

        _defaultScale = defaultScale;
        _wheelScrollLines = wheelScrollLines;
        _fontManager = fontManager;
        _textLayoutEngine = textLayoutEngine;
        _clipboard = clipboard;
        _logger = logger;
#if DEBUG
        _ownerThreadId = Environment.CurrentManagedThreadId;
#endif
        _isInitialized = true;
        lock (PostSync)
            _acceptingPosts = true;
    }

    /// <summary>
    /// Releases the host's service bindings after all active UI use has ended, without destroying services or resetting settings.
    /// </summary>
    /// <remarks>
    /// The host must close all scenes and finish UI frame callbacks before calling this method.
    /// Do not call from a posted action or ticker callback. Tickers from this runtime must not be reused after shutdown.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown in Debug builds when called from another thread while initialized.</exception>
    public static void Shutdown()
    {
        if (!_isInitialized)
            return;
        VerifyAccess();

        lock (PostSync)
        {
            _acceptingPosts = false;
            PendingActions.Clear();
        }

        while (Tickers.First is { } registration)
            registration.Value.StopCore();

        _fontManager = null;
        _textLayoutEngine = null;
        _clipboard = null;
        _logger = null;
#if DEBUG
        _ownerThreadId = 0;
#endif
        _isInitialized = false;
    }

    internal static LinkedListNode<Ticker> RegisterTicker(Ticker ticker) => Tickers.AddLast(ticker);

    internal static void UnregisterTicker(LinkedListNode<Ticker> registration) => Tickers.Remove(registration);

    private static void DrainPending()
    {
        int batchSize;
        lock (PostSync)
            batchSize = PendingActions.Count;

        for (var index = 0; index < batchSize; index++)
        {
            Action action;
            lock (PostSync)
                action = PendingActions.Dequeue();
            action();
        }
    }

    private static LinkedListNode<Ticker>[] FreezeTickers()
    {
        var registrations = new LinkedListNode<Ticker>[Tickers.Count];
        var index = 0;
        for (var registration = Tickers.First; registration is not null; registration = registration.Next)
            registrations[index++] = registration;
        return registrations;
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
            throw new ArgumentOutOfRangeException(parameterName,
                "Wheel scroll lines must be minus one or non-negative.");
    }

    internal static void VerifyInitialized()
    {
        if (!_isInitialized)
            throw new InvalidOperationException("UI toolkit services have not been initialized.");
    }

    internal static void VerifyAccess()
    {
#if DEBUG
        if (_isInitialized && _ownerThreadId != Environment.CurrentManagedThreadId)
            throw new InvalidOperationException("UI access requires the host UI thread.");
#endif
    }
}

using PanguEngine.Input;

namespace PanguEngine.Client.UI;

/// <summary>
/// Represents a UI screen with client game policies and update callbacks.
/// </summary>
public class GameScreen : UiScreen
{
    /// <summary>
    /// Initializes a game screen with an optional root node.
    /// </summary>
    /// <param name="root">The initial root node, or null to create an empty screen.</param>
    public GameScreen(UiNode? root = null) : base(root)
    {
    }

    /// <summary>
    /// Gets the input context active while this screen is current.
    /// </summary>
    public InputContext InputContext { get; init; } = BuiltinInputContexts.Ui;

    /// <summary>
    /// Gets whether the game host pauses the game while this screen is current.
    /// </summary>
    public bool PausesGame { get; init; }

    /// <summary>
    /// Gets whether the game host closes this screen when Escape is pressed.
    /// </summary>
    public bool CloseOnEscape { get; init; }

    /// <summary>Updates the screen at the client's fixed update frequency.</summary>
    protected virtual void OnFixedUpdate()
    {
    }

    /// <summary>Updates the screen before layout for a client frame.</summary>
    /// <param name="alpha">The interpolation factor between fixed updates.</param>
    protected virtual void OnFrameUpdate(double alpha)
    {
    }

    internal void Update()
    {
        VerifyOwnerThread();
        BeginUpdate();
        try
        {
            if (IsScreenActive())
                OnFixedUpdate();
        }
        finally
        {
            EndUpdate();
        }
    }

    internal void UpdateFrame(double alpha)
    {
        VerifyOwnerThread();
        BeginUpdate();
        try
        {
            if (IsScreenActive())
                OnFrameUpdate(alpha);
        }
        finally
        {
            EndUpdate();
        }
    }
}

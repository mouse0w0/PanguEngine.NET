using PanguEngine.Input;
using PanguEngine.UI;

namespace PanguEngine.Client.UI;

/// <summary>
/// Represents a UI scene with client game policies and update callbacks.
/// </summary>
public class GameUIScene : UIScene
{
    /// <summary>
    /// Initializes a game UI scene with an optional root element.
    /// </summary>
    /// <param name="root">The initial root element, or null to create an empty scene.</param>
    public GameUIScene(UIElement? root = null) : base(root)
    {
    }

    /// <summary>
    /// Gets the input context active while this scene is current.
    /// </summary>
    public InputContext InputContext { get; init; } = BuiltinInputContexts.Ui;

    /// <summary>
    /// Gets whether the game host pauses the game while this scene is current.
    /// </summary>
    public bool PausesGame { get; init; }

    /// <summary>
    /// Gets whether the game host closes this scene when Escape is pressed.
    /// </summary>
    public bool CloseOnEscape { get; init; }

    /// <summary>Updates the scene at the client's fixed update frequency.</summary>
    protected virtual void OnFixedUpdate()
    {
    }

    /// <summary>Updates the scene before layout for a client frame.</summary>
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
            if (IsSceneActive())
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
            if (IsSceneActive())
                OnFrameUpdate(alpha);
        }
        finally
        {
            EndUpdate();
        }
    }
}
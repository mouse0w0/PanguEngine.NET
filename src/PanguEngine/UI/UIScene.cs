using PanguEngine.ComponentModel;
using PanguEngine.UI.Styling;

namespace PanguEngine.UI;

/// <summary>
/// Represents a retained UI scene with an optional root element.
/// </summary>
/// <remarks>
/// The root and its owned tree cannot change while this scene is generating drawing commands.
/// Callback failures propagate immediately without rolling back completed changes or invoking later callbacks.
/// Open scenes share the host UI thread established by UIToolkit. Thread diagnostics are enabled only in Debug builds.
/// </remarks>
public partial class UIScene : ObservableObject
{
    /// <summary>Identifies the read-only property indicating whether this scene is open.</summary>
    public static readonly Property<bool> IsOpenProperty =
        Property.RegisterDirect<UIScene, bool>(nameof(IsOpen), static scene => scene.IsOpen);

    private const int MaxLayoutPasses = 16;
    private bool _isClosing;

    private UIElement? _root;
    private StyleResolver _styleResolver = StyleResolver.Default;
    private int _operationDepth;
    private bool _isInteractionActive;
    private bool _isTransitioning;
    private bool _isPreparingStyleSheets;

    /// <summary>
    /// Initializes a UI scene with an optional root element.
    /// </summary>
    /// <param name="root">The initial root element, or null to create an empty scene.</param>
    public UIScene(UIElement? root = null)
    {
        _scale = UIToolkit.DefaultScale;
        Root = root;
    }

    /// <summary>
    /// Gets or sets the root element of this scene.
    /// </summary>
    /// <remarks>
    /// Assigning an element moves it from its current parent or scene. Closing the scene preserves
    /// the root and its scene ownership.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown during conflicting layout or drawing operations, or in Debug builds when an involved open scene
    /// is accessed from outside the host UI thread.
    /// </exception>
    public UIElement? Root
    {
        get => _root;
        set => SetRoot(value);
    }

    /// <summary>
    /// Invoked before this scene opens.
    /// </summary>
    protected virtual void OnOpening()
    {
    }

    /// <summary>
    /// Invoked after this scene opens.
    /// </summary>
    protected virtual void OnOpened()
    {
    }

    /// <summary>
    /// Invoked before this scene closes.
    /// </summary>
    protected virtual void OnClosing()
    {
    }

    /// <summary>
    /// Invoked after this scene closes.
    /// </summary>
    protected virtual void OnClosed()
    {
    }

    /// <summary>
    /// Opens this scene on the initialized host UI thread.
    /// </summary>
    /// <remarks>
    /// The host owns frame preparation, input forwarding, and closure.
    /// UIToolkit must be initialized before opening the scene.
    /// Scenes managed by a UI manager must be opened through that manager.
    /// Callback failures propagate without rolling back completed changes.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when UIToolkit is not initialized, the scene is already open, a previous operation or lifecycle
    /// transition has not completed, or layout or stylesheet preparation is in progress.
    /// Also thrown in Debug builds when called from outside the host UI thread.
    /// </exception>
    public void Open()
    {
        VerifyCanOpenCore();
        try
        {
            BeginOpening();
            SynchronizeDefaultScale();
            OnOpening();
            ActivateInteraction();
            OnOpened();
        }
        finally
        {
            EndLifecycleTransition();
        }
    }

    /// <summary>
    /// Closes this scene while preserving its root and tree ownership.
    /// </summary>
    /// <remarks>
    /// Closing a fully closed scene has no effect. An open or closing scene requires the host UI thread.
    /// Scenes managed by a UI manager must be closed through that manager.
    /// Callback failures propagate immediately without invoking later callbacks or rolling back completed changes.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown during conflicting lifecycle, layout, stylesheet preparation, or drawing operations.
    /// Also thrown in Debug builds when an open or closing scene is accessed from outside the host UI thread.
    /// </exception>
    public void Close()
    {
        if (!IsOpen && !_isClosing)
            return;

        VerifyCanClose();
        try
        {
            BeginClosing();
            CloseCore();
        }
        finally
        {
            EndLifecycleTransition();
        }
    }

    internal bool IsUpdating { get; private set; }

    /// <summary>
    /// Updates styles and layout for this scene.
    /// </summary>
    /// <param name="viewportSize">The viewport size in output coordinates.</param>
    /// <remarks>
    /// Call on the host UI thread after the toolkit update and game frame callbacks, before recording drawing commands.
    /// Scenes managed by a UI manager receive frame preparation from that manager.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the scene is not open, is changing lifecycle state, is already updating or drawing,
    /// or its layout does not stabilize. Also thrown in Debug builds when accessed from outside the host UI thread.
    /// </exception>
    public void PrepareFrame(Size viewportSize)
    {
        CreateViewportBounds(viewportSize);
        BeginUpdate();
        try
        {
            if (IsSceneActive())
                UpdateLayout(viewportSize);
        }
        finally
        {
            EndUpdate();
        }
    }

    /// <summary>Begins an update operation on this scene.</summary>
    private protected void BeginUpdate()
    {
        VerifyNotTransitioningOrUpdatingLayout();
        if (IsUpdating)
            throw new InvalidOperationException("The UI scene is already updating.");

        BeginRuntimeOperation();
        IsUpdating = true;
    }

    /// <summary>Ends the current update operation.</summary>
    private protected void EndUpdate()
    {
        IsUpdating = false;
        EndRuntimeOperation();
    }

    private void UpdateLayout(Size viewportSize)
    {
        SynchronizeDefaultScale();
        for (var pass = 0; pass < MaxLayoutPasses; pass++)
        {
            if (!IsSceneActive() || Root is not { } root)
                return;

            var scale = Scale;
            var logicalViewportSize = new Size(viewportSize.Width / scale, viewportSize.Height / scale);
            var viewportBounds = CreateViewportBounds(logicalViewportSize);

            IsUpdatingLayout = true;
            try
            {
                root.UpdateStyles();
                root.Measure(logicalViewportSize);
                if (!root.IsStyleSubtreeValid || !root.IsMeasureValid) continue;

                root.Arrange(viewportBounds);
            }
            finally
            {
                IsUpdatingLayout = false;
            }

            if (!root.IsStyleSubtreeValid || !root.IsMeasureValid || !root.IsArrangeValid)
                continue;

            RefreshPointerAfterLayout();
            if (!IsSceneActive() || Root is null)
                return;

            if (ReferenceEquals(Root, root) && root.IsStyleSubtreeValid && root.IsMeasureValid && root.IsArrangeValid)
                return;
        }

        var stage = Root is { IsStyleSubtreeValid: false } ? "Style"
            : Root is { IsMeasureValid: false } ? "Measure"
            : Root is { IsArrangeValid: false } ? "Arrange"
            : "Root/Scale coordination";
        throw new InvalidOperationException(
            $"UI layout did not stabilize for {GetType().FullName} after {MaxLayoutPasses} passes ({stage}).");
    }

    internal void VerifyOwnerThread() => VerifyOwnerThreadCore();

    internal void VerifyTreeAccess()
    {
        if (IsOpen || _isClosing)
            VerifyOwnerThreadCore();
    }

    /// <summary>
    /// Gets whether this scene has entered its open state and has not begun closing.
    /// </summary>
    /// <remarks>
    /// This does not indicate whether opening callbacks completed successfully or the scene is accepting input.
    /// Query on the host UI thread, or with serialized access while offline. Concurrent background queries are not synchronized.
    /// </remarks>
    public bool IsOpen { get; private set; }

    internal void VerifyCanOpen() => VerifyCanOpenCore();

    internal void VerifyCanClose()
    {
        VerifyOwnerThread();
        VerifyNotTransitioningOrUpdatingLayout();
    }

    internal bool IsUpdatingLayout { get; private set; }

    private void SetRoot(UIElement? root)
    {
        if (ReferenceEquals(_root, root))
            return;

        var sourceScene = root?.Scene;
        var rootOriginalParent = root?.Parent;
        var targetOperation = false;
        var sourceOperation = false;
        InputStateCleanupSnapshot? targetSnapshot = null;
        InputStateCleanupSnapshot? sourceSnapshot = null;
        try
        {
            targetOperation = BeginRootTransferOperation();
            if (sourceScene is not null && !ReferenceEquals(sourceScene, this))
                sourceOperation = sourceScene.BeginRootTransferOperation();

            if (root is not null)
            {
                if (rootOriginalParent is not null)
                {
                    rootOriginalParent.Children.Remove(root);
                    if (root.Parent is not null || root.Scene is not null)
                        throw new InvalidOperationException("The UI node was attached during removal notification.");
                }

                if (sourceScene is not null && ReferenceEquals(sourceScene._root, root))
                    sourceScene._root = null;
            }

            var oldRoot = _root;
            var oldRootScene = oldRoot?.Scene;
            _root = null;
            oldRoot?.SetSceneRecursive(null);
            _root = root;
            root?.SetSceneRecursive(this);
            if (oldRoot is not null && !ReferenceEquals(oldRootScene, oldRoot.Scene))
                oldRoot.InvalidateMeasureSubtree();
            if (root is not null && !ReferenceEquals(sourceScene, root.Scene))
                root.InvalidateMeasureSubtree();
            oldRoot?.InvalidateTreeStructure();
            root?.InvalidateTreeStructure();

            oldRoot?.InvalidateStyleSubtree();
            root?.InvalidateStyleSubtree();

            if (targetOperation)
                targetSnapshot = CommitInputStateAfterTreeChange();
            if (sourceOperation)
                sourceSnapshot = sourceScene!.CommitInputStateAfterTreeChange();

            if (targetSnapshot is not null)
                NotifyInputStateLoss(targetSnapshot);

            if (sourceSnapshot is not null)
                sourceScene!.NotifyInputStateLoss(sourceSnapshot);
        }
        finally
        {
            if (sourceOperation)
                sourceScene!.EndRuntimeOperation();
            if (targetOperation)
                EndRuntimeOperation();
        }
    }

    private bool BeginRootTransferOperation()
    {
        VerifyNotPreparingStyleSheets();
        if (IsOpen || _isClosing)
            VerifyOwnerThreadCore();
        if (IsDrawing)
        {
            throw new InvalidOperationException(
                "The UI scene root cannot change while drawing commands are generated.");
        }

        if (IsUpdatingLayout)
            throw new InvalidOperationException("The UI scene root cannot change during layout.");
        if (!IsOpen && !_isClosing)
            return false;
        _operationDepth++;
        return true;
    }

    private void BeginOpening()
    {
        _isTransitioning = true;
        _isInteractionActive = false;
        _isClosing = false;
        IsOpen = true;
        RaisePropertyChanged(IsOpenProperty, false, true);
    }

    private void VerifyCanOpenCore()
    {
        UIToolkit.VerifyInitialized();
        UIToolkit.VerifyAccess();
        VerifyNotPreparingStyleSheets();
        if (IsUpdatingLayout)
            throw new InvalidOperationException("The UI scene cannot open during layout or style application.");
        if (IsOpen || _isClosing)
            throw new InvalidOperationException("The UI scene is already open or has not completed closing.");
        if (_isTransitioning || _operationDepth != 0)
            throw new InvalidOperationException("The previous UI scene operation has not completed.");
    }

    private void VerifyOwnerThreadCore()
    {
        if (!IsOpen && !_isClosing)
            throw new InvalidOperationException("The UI scene is not open or closing.");
        UIToolkit.VerifyAccess();
    }

    private void BeginRuntimeOperation()
    {
        VerifyOwnerThreadCore();
        _operationDepth++;
    }

    internal bool BeginRuntimeOperationIfOpen()
    {
        if (!IsOpen && !_isClosing)
            return false;

        VerifyOwnerThreadCore();
        _operationDepth++;
        return true;
    }

    internal void EndRuntimeOperation() => _operationDepth--;

    private void EndLifecycleTransition() => _isTransitioning = false;

    private void ActivateInteraction() => _isInteractionActive = true;

    private void BeginClosing()
    {
        _isTransitioning = true;
        _isInteractionActive = false;
        MarkClosing();
    }

    internal void DeactivateForClose()
    {
        VerifyOwnerThreadCore();
        MarkClosing();
    }

    private void MarkClosing()
    {
        var wasOpen = IsOpen;
        _isClosing = true;
        IsOpen = false;
        if (wasOpen)
            RaisePropertyChanged(IsOpenProperty, true, false);
    }

    private void CloseCore()
    {
        OnClosing();
        CancelPendingPointerInput();
        OnClosed();
        ResetOpenState();
    }

    private void ResetOpenState()
    {
        _isInteractionActive = false;
        _isClosing = false;
    }

    private void VerifyNotTransitioningOrUpdatingLayout()
    {
        VerifyNotPreparingStyleSheets();
        if (_isTransitioning)
            throw new InvalidOperationException("The UI scene is already changing lifecycle state.");
        if (IsUpdatingLayout)
            throw new InvalidOperationException("The UI scene cannot change lifecycle state during layout.");
        if (IsDrawing)
        {
            throw new InvalidOperationException(
                "The UI scene cannot change lifecycle state while drawing commands are generated.");
        }
    }

    internal static Rect CreateViewportBounds(Size viewportSize) =>
        new(0, 0, viewportSize);
}

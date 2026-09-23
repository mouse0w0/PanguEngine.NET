using PanguEngine.Client.UI.Styling;
using PanguEngine.Input;

namespace PanguEngine.Client.UI;

/// <summary>
/// Represents a retained UI screen with an optional root node.
/// </summary>
/// <remarks>
/// The root and its owned tree cannot change while this screen is generating drawing commands.
/// Callback failures propagate immediately without rolling back completed changes or invoking later callbacks.
/// </remarks>
public partial class UiScreen
{
    private readonly Lock _stateSync = new();
    private readonly Queue<Action> _pendingActions = [];
    private UiNode? _root;
    private UiStyleResolver _styleResolver = UiStyleResolver.Default;
    private int? _ownerThreadId;
    private int _operationDepth;
    private bool _isClosing;
    private bool _isDraining;
    private bool _isInteractionActive;
    private bool _isTransitioning;
    private bool _isApplyingStyleSheets;
    private bool _isPreparingStyleSheets;

    /// <summary>
    /// Initializes a UI screen with an optional root node.
    /// </summary>
    /// <param name="root">The initial root node, or null to create an empty screen.</param>
    public UiScreen(UiNode? root = null)
    {
        _scale = UiSettings.DefaultScale;
        Root = root;
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

    /// <summary>
    /// Gets or sets the root node of this screen.
    /// </summary>
    /// <remarks>
    /// Assigning a node moves it from its current parent or screen. Closing the screen preserves
    /// the root and its screen ownership.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when an involved open screen is accessed from the wrong thread, is updating layout,
    /// or is generating drawing commands.
    /// </exception>
    public UiNode? Root
    {
        get => _root;
        set => SetRoot(value);
    }

    /// <summary>
    /// Posts an action for execution during the next frame preparation of the open screen.
    /// </summary>
    /// <param name="action">The action to enqueue.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the screen is not open or has entered its close transition.
    /// </exception>
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        lock (_stateSync)
        {
            if (_ownerThreadId is null || _isClosing)
                throw new InvalidOperationException("The UI screen is not accepting posted actions.");

            _pendingActions.Enqueue(action);
        }
    }

    /// <summary>
    /// Invoked before this screen opens.
    /// </summary>
    protected virtual void OnOpening()
    {
    }

    /// <summary>
    /// Invoked after this screen opens.
    /// </summary>
    protected virtual void OnOpened()
    {
    }

    /// <summary>
    /// Invoked before this screen closes.
    /// </summary>
    protected virtual void OnClosing()
    {
    }

    /// <summary>
    /// Invoked after this screen closes.
    /// </summary>
    protected virtual void OnClosed()
    {
    }

    /// <summary>Updates the screen at the client's fixed update frequency.</summary>
    protected virtual void OnFixedUpdate()
    {
    }

    /// <summary>Updates the screen before layout for a client frame.</summary>
    /// <param name="alpha">The interpolation factor between fixed updates.</param>
    protected virtual void OnFrameUpdate(double alpha)
    {
    }

    internal void Open()
    {
        BindOwnerForOpen();
        try
        {
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

    internal void Close()
    {
        if (!IsOpen())
            return;

        VerifyCanClose();
        BeginClosing();
        try
        {
            CloseCore();
        }
        finally
        {
            EndLifecycleTransition();
        }
    }

    internal bool IsUpdating { get; private set; }

    internal void Update()
    {
        VerifyOwnerThread();
        VerifyNotTransitioningOrUpdatingLayout();
        if (IsUpdating)
            throw new InvalidOperationException("The UI screen is already updating.");

        BeginRuntimeOperation();
        IsUpdating = true;
        try
        {
            if (IsScreenActive())
                OnFixedUpdate();
        }
        finally
        {
            IsUpdating = false;
            EndRuntimeOperation();
        }
    }

    internal void PrepareFrame(Size viewportSize, double alpha)
    {
        CreateViewportBounds(viewportSize);
        VerifyOwnerThread();
        VerifyNotTransitioningOrUpdatingLayout();
        if (IsUpdating)
            throw new InvalidOperationException("The UI screen is already updating.");

        BeginRuntimeOperation();
        IsUpdating = true;
        try
        {
            DrainPending();
            if (IsScreenActive())
                OnFrameUpdate(alpha);
            if (IsScreenActive())
                UpdateLayout(viewportSize);
        }
        finally
        {
            IsUpdating = false;
            EndRuntimeOperation();
        }
    }

    private void UpdateLayout(Size viewportSize)
    {
        SynchronizeDefaultScale();
        var scale = Scale;
        var logicalViewportSize = new Size(
            viewportSize.Width / scale,
            viewportSize.Height / scale);
        var viewportBounds = CreateViewportBounds(logicalViewportSize);
        var root = Root;
        if (root is null)
            return;

        IsUpdatingLayout = true;
        try
        {
            root.Measure(logicalViewportSize);
            if (root.IsMeasureValid)
                root.Arrange(viewportBounds);
        }
        finally
        {
            IsUpdatingLayout = false;
        }

        if (IsScreenActive())
            RefreshPointerAfterLayout();
    }

    internal void VerifyOwnerThread()
    {
        lock (_stateSync)
            VerifyOwnerThreadCore();
    }

    internal void VerifyTreeAccess()
    {
        lock (_stateSync)
        {
            if (_ownerThreadId is not null)
                VerifyOwnerThreadCore();
        }
    }

    internal bool IsOpen()
    {
        lock (_stateSync)
            return _ownerThreadId is not null;
    }

    internal void VerifyCanOpen()
    {
        lock (_stateSync)
            VerifyCanOpenCore();
    }

    internal void VerifyCanClose()
    {
        VerifyOwnerThread();
        VerifyNotTransitioningOrUpdatingLayout();
    }

    internal bool IsUpdatingLayout { get; private set; }

    internal bool IsApplyingStyleSheets
    {
        get
        {
            lock (_stateSync)
                return _isApplyingStyleSheets;
        }
    }

    private void SetRoot(UiNode? root)
    {
        if (ReferenceEquals(_root, root))
            return;

        var sourceScreen = root?.Screen;
        var targetOperation = false;
        var sourceOperation = false;
        InputStateCleanupSnapshot? targetSnapshot = null;
        InputStateCleanupSnapshot? sourceSnapshot = null;
        try
        {
            targetOperation = BeginRootTransferOperation();
            if (sourceScreen is not null && !ReferenceEquals(sourceScreen, this))
                sourceOperation = sourceScreen.BeginRootTransferOperation();

            var oldRoot = _root;
            var oldRootScreen = oldRoot?.Screen;
            if (root is not null)
            {
                root.Parent?.RemoveChildForRootTransfer(root);
                if (sourceScreen is not null && ReferenceEquals(sourceScreen._root, root))
                    sourceScreen.ClearRootForTransfer();
            }

            _root = null;
            oldRoot?.SetScreenRecursive(null);
            _root = root;
            root?.SetScreenRecursive(this);
            if (oldRoot is not null && !ReferenceEquals(oldRootScreen, oldRoot.Screen))
                oldRoot.InvalidateMeasureSubtree();
            if (root is not null && !ReferenceEquals(sourceScreen, root.Screen))
                root.InvalidateMeasureSubtree();
            oldRoot?.InvalidateTreeStructure();
            root?.InvalidateTreeStructure();

            var styleEntries = new (UiNode? Root, UiStyleResolver Resolver)[]
            {
                (oldRoot, UiStyleResolver.Default),
                (root, _styleResolver)
            };
            UiNode.RecomputeStyleSubtreeBatch(styleEntries);

            if (targetOperation)
                targetSnapshot = CommitInputStateAfterTreeChange();
            if (sourceOperation)
                sourceSnapshot = sourceScreen!.CommitInputStateAfterTreeChange();

            if (targetSnapshot is not null)
                NotifyInputStateLoss(targetSnapshot);

            if (sourceSnapshot is not null)
                sourceScreen!.NotifyInputStateLoss(sourceSnapshot);
        }
        finally
        {
            if (sourceOperation)
                sourceScreen!.EndRuntimeOperation();
            if (targetOperation)
                EndRuntimeOperation();
        }
    }

    private bool BeginRootTransferOperation()
    {
        lock (_stateSync)
        {
            VerifyNotPreparingStyleSheets();
            if (_ownerThreadId is not null)
                VerifyOwnerThreadCore();
            if (_isDrawing)
            {
                throw new InvalidOperationException(
                    "The UI screen root cannot change while drawing commands are generated.");
            }

            if (_ownerThreadId is null)
                return false;

            if (IsUpdatingLayout)
                throw new InvalidOperationException("The UI screen root cannot change during layout.");
            _operationDepth++;
            return true;
        }
    }

    internal void ClearRootForTransfer()
    {
        _root = null;
    }

    private void BindOwnerForOpen()
    {
        lock (_stateSync)
        {
            VerifyCanOpenCore();
            _ownerThreadId = Environment.CurrentManagedThreadId;
            _isClosing = false;
            _isInteractionActive = false;
            _isTransitioning = true;
            _pendingActions.Clear();
        }
    }

    private void VerifyCanOpenCore()
    {
        VerifyNotPreparingStyleSheets();
        if (_ownerThreadId is not null)
            throw new InvalidOperationException("The UI screen is already open.");
        if (_isTransitioning || _operationDepth != 0)
            throw new InvalidOperationException("The previous UI screen operation has not completed.");
    }

    private void VerifyOwnerThreadCore()
    {
        var ownerThreadId = _ownerThreadId;
        if (ownerThreadId is null)
            throw new InvalidOperationException("The UI screen is not open.");
        if (ownerThreadId != Environment.CurrentManagedThreadId)
            throw new InvalidOperationException("UI screen access requires its owner thread.");
    }

    private void BeginRuntimeOperation()
    {
        lock (_stateSync)
        {
            VerifyOwnerThreadCore();
            _operationDepth++;
        }
    }

    internal bool BeginRuntimeOperationIfOpen()
    {
        lock (_stateSync)
        {
            if (_ownerThreadId is null)
                return false;

            VerifyOwnerThreadCore();
            _operationDepth++;
            return true;
        }
    }

    internal void EndRuntimeOperation()
    {
        lock (_stateSync)
            _operationDepth--;
    }

    private void EndLifecycleTransition()
    {
        lock (_stateSync)
            _isTransitioning = false;
    }

    private void ActivateInteraction()
    {
        lock (_stateSync)
            _isInteractionActive = true;
    }

    private void BeginClosing()
    {
        lock (_stateSync)
        {
            _isTransitioning = true;
            _isClosing = true;
            _isInteractionActive = false;
            _pendingActions.Clear();
        }
    }

    internal void StopAcceptingPosts()
    {
        lock (_stateSync)
        {
            VerifyOwnerThreadCore();
            _isClosing = true;
            _pendingActions.Clear();
        }
    }

    private void DrainPending()
    {
        VerifyOwnerThread();
        int batchSize;
        lock (_stateSync)
        {
            if (_isDraining)
                throw new InvalidOperationException("The UI screen is already draining posted actions.");

            _isDraining = true;
            batchSize = _pendingActions.Count;
        }

        try
        {
            for (var index = 0; index < batchSize; index++)
            {
                Action action;
                lock (_stateSync)
                {
                    if (_pendingActions.Count == 0)
                        break;
                    action = _pendingActions.Dequeue();
                }

                action();
            }
        }
        finally
        {
            lock (_stateSync)
                _isDraining = false;
        }
    }

    private void CloseCore()
    {
        OnClosing();
        var snapshot = CommitInputStateForClose();
        if (snapshot is not null)
            NotifyInputStateLoss(snapshot);
        OnClosed();
        ResetOpenState();
    }

    private void ResetOpenState()
    {
        lock (_stateSync)
        {
            _pendingActions.Clear();
            _isClosing = false;
            _isInteractionActive = false;
            _ownerThreadId = null;
        }
    }

    private void VerifyNotTransitioningOrUpdatingLayout()
    {
        lock (_stateSync)
        {
            VerifyNotPreparingStyleSheets();
            if (_isTransitioning)
                throw new InvalidOperationException("The UI screen is already changing lifecycle state.");
            if (IsUpdatingLayout)
                throw new InvalidOperationException("The UI screen cannot change lifecycle state during layout.");
            if (_isDrawing)
            {
                throw new InvalidOperationException(
                    "The UI screen cannot change lifecycle state while drawing commands are generated.");
            }
        }
    }

    internal static Rect CreateViewportBounds(Size viewportSize) =>
        new(0, 0, viewportSize);
}
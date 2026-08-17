using System.Runtime.ExceptionServices;

namespace PanguEngine.Client.UI;

/// <summary>
/// Represents a retained UI screen with an optional root node.
/// </summary>
/// <remarks>
/// The root and its owned tree cannot change while this screen is generating drawing commands.
/// </remarks>
public partial class UiScreen
{
    private readonly Lock _stateSync = new();
    private readonly Queue<Action> _pendingActions = [];
    private UiNode? _root;
    private int? _ownerThreadId;
    private int _operationDepth;
    private bool _isClosing;
    private bool _isDraining;
    private bool _isInteractionActive;
    private bool _isTransitioning;

    /// <summary>
    /// Initializes a UI screen with an optional root node.
    /// </summary>
    /// <param name="root">The initial root node, or null to create an empty screen.</param>
    public UiScreen(UiNode? root = null)
    {
        Root = root;
    }

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
    /// Posts an action for execution on the next update of the open screen.
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

    internal void Open()
    {
        BindOwnerForOpen();
        try
        {
            try
            {
                OnOpening();
                ActivateInteraction();
            }
            catch
            {
                ResetOpenState();
                throw;
            }

            try
            {
                OnOpened();
            }
            catch (Exception exception)
            {
                var errors = new List<Exception> { exception };
                BeginClosing();
                CloseCore(errors);
                ThrowLifecycleErrors(errors);
            }
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
            var errors = new List<Exception>();
            CloseCore(errors);
            ThrowLifecycleErrors(errors);
        }
        finally
        {
            EndLifecycleTransition();
        }
    }

    internal void Update(Size viewportSize)
    {
        CreateViewportBounds(viewportSize);
        VerifyOwnerThread();
        VerifyNotTransitioningOrUpdatingLayout();
        BeginRuntimeOperation();
        try
        {
            DrainPending();
            if (!IsScreenActive())
                return;

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
        finally
        {
            EndRuntimeOperation();
        }
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

    private void SetRoot(UiNode? root)
    {
        if (ReferenceEquals(_root, root))
            return;

        var sourceScreen = root?.Screen;
        var targetOperation = false;
        var sourceOperation = false;
        InputStateCleanupSnapshot? targetSnapshot = null;
        InputStateCleanupSnapshot? sourceSnapshot = null;
        var errors = new List<Exception>();
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

            if (targetOperation)
                targetSnapshot = CommitInputStateAfterTreeChange();
            if (sourceOperation)
                sourceSnapshot = sourceScreen!.CommitInputStateAfterTreeChange();

            if (targetSnapshot is not null)
            {
                try
                {
                    NotifyInputStateLoss(targetSnapshot);
                }
                catch (Exception exception)
                {
                    AddLifecycleErrors(errors, exception);
                }
            }

            if (sourceSnapshot is not null)
            {
                try
                {
                    sourceScreen!.NotifyInputStateLoss(sourceSnapshot);
                }
                catch (Exception exception)
                {
                    AddLifecycleErrors(errors, exception);
                }
            }
        }
        finally
        {
            if (sourceOperation)
                sourceScreen!.EndRuntimeOperation();
            if (targetOperation)
                EndRuntimeOperation();
        }

        ThrowLifecycleErrors(errors);
    }

    private bool BeginRootTransferOperation()
    {
        lock (_stateSync)
        {
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

    private void CloseCore(List<Exception> errors)
    {
        try
        {
            OnClosing();
        }
        catch (Exception exception)
        {
            AddLifecycleErrors(errors, exception);
        }

        try
        {
            var snapshot = CommitInputStateForClose();
            if (snapshot is not null)
                NotifyInputStateLoss(snapshot);
        }
        catch (Exception exception)
        {
            AddLifecycleErrors(errors, exception);
        }

        try
        {
            OnClosed();
        }
        catch (Exception exception)
        {
            AddLifecycleErrors(errors, exception);
        }
        finally
        {
            ResetOpenState();
        }
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

    private static void ThrowLifecycleErrors(List<Exception> errors)
    {
        if (errors.Count == 1)
            ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1)
            throw new AggregateException(errors);
    }

    private static void AddLifecycleErrors(List<Exception> errors, Exception exception)
    {
        errors.AddRange(exception switch
        {
            AggregateException aggregate => aggregate.InnerExceptions,
            _ => [exception]
        });
    }
}

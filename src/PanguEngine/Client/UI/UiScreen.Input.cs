using PanguEngine.Input;
using System.Runtime.ExceptionServices;

namespace PanguEngine.Client.UI;

public partial class UiScreen
{
    private readonly List<UiHitPathEntry> _hoverPath = [];
    private readonly UiNode?[] _pressedTargets =
        new UiNode?[(int)MouseButton.Button12 - (int)MouseButton.Left + 1];
    private Control[]? _leftPressedControls;
    private Point _pointerOutputPosition;
    private Point _pointerPosition;
    private bool _hasPointerPosition;
    private bool _isChangingFocus;
    private bool _isRoutingInput;

    internal sealed class InputStateCleanupSnapshot
    {
        internal InputStateCleanupSnapshot(
            UiNode? focusedNode,
            Control[] pressedControls,
            UiHitPathEntry[] hoverPath,
            UiNode[] exitedNodes,
            Point pointerPosition)
        {
            FocusedNode = focusedNode;
            PressedControls = pressedControls;
            HoverPath = hoverPath;
            ExitedNodes = exitedNodes;
            PointerPosition = pointerPosition;
        }

        internal UiNode? FocusedNode { get; }
        internal Control[] PressedControls { get; }
        internal UiHitPathEntry[] HoverPath { get; }
        internal UiNode[] ExitedNodes { get; }
        internal Point PointerPosition { get; }
    }

    /// <summary>
    /// Gets the node that currently owns keyboard focus.
    /// </summary>
    public UiNode? FocusedNode { get; private set; }

    /// <summary>
    /// Finds the frontmost deepest node at a point in screen logical coordinates.
    /// </summary>
    /// <param name="screenPoint">The point in screen logical coordinates.</param>
    /// <returns>The deepest hit node, or null when the root subtree is not hit.</returns>
    public UiNode? HitTest(Point screenPoint)
    {
        var path = BuildHitPath(screenPoint);
        return path.Count == 0 ? null : path[^1].Node;
    }

    /// <summary>
    /// Clears keyboard focus using a complete focus transition.
    /// </summary>
    public void ClearFocus()
    {
        if (!IsScreenActive())
            return;

        VerifyOwnerThread();
        _ = ChangeFocus(null);
    }

    internal bool TryFocus(UiNode node)
    {
        if (!IsScreenActive())
            return false;

        VerifyOwnerThread();
        if (!CanFocus(node))
            return false;

        return ChangeFocus(node);
    }

    internal void RefreshPointerAfterLayout()
    {
        VerifyOwnerThread();
        VerifyNotTransitioningOrUpdatingLayout();
        if (!IsScreenActive() || !_hasPointerPosition)
            return;

        BeginInputRouting();
        try
        {
            UpdateHover(ToLogicalPoint(_pointerOutputPosition));
        }
        finally
        {
            EndInputRouting();
        }
    }

    internal void ProcessPointerMoved(Point position)
    {
        BeginInputRouting();
        try
        {
            var logicalPosition = UpdatePointerPosition(position);
            UpdateHover(logicalPosition);
            if (!IsScreenActive() || _hoverPath.Count == 0)
                return;

            var path = _hoverPath.ToArray();
            var args = new UiPointerEventArgs(path[^1].Node, logicalPosition, path);
            Bubble(
                path,
                args,
                static (node, eventArgs) => node.RaisePointerMoved(eventArgs));
        }
        finally
        {
            EndInputRouting();
        }
    }

    internal void ProcessPointerPressed(
        Point position,
        MouseButton button,
        KeyModifiers modifiers)
    {
        BeginInputRouting(button);
        try
        {
            var logicalPosition = UpdatePointerPosition(position);
            UpdateHover(logicalPosition);
            if (!IsScreenActive())
                return;

            var path = _hoverPath.ToArray();
            var index = GetButtonIndex(button);
            var target = path.Length == 0 ? null : path[^1].Node;
            Control[]? oldPressedControls = null;
            Control[]? newPressedControls = null;
            _pressedTargets[index] = target;
            if (button == MouseButton.Left)
            {
                oldPressedControls = _leftPressedControls;
                var controls = GetControls(path);
                newPressedControls = controls.Length == 0 ? null : controls;
                _leftPressedControls = newPressedControls;

                var errors = new List<Exception>();
                ProjectPressedDifference(oldPressedControls, newPressedControls, errors);
                ThrowInputErrors(errors);
            }

            UiNode? focusCandidate = null;
            for (var pathIndex = path.Length - 1; pathIndex >= 0; pathIndex--)
            {
                if (CanFocus(path[pathIndex].Node))
                {
                    focusCandidate = path[pathIndex].Node;
                    break;
                }
            }

            _ = ChangeFocus(focusCandidate);
            if (!IsScreenActive() || path.Length == 0)
                return;

            var args = new UiPointerButtonEventArgs(
                path[^1].Node,
                logicalPosition,
                button,
                modifiers,
                path);
            Bubble(
                path,
                args,
                static (node, eventArgs) => node.RaisePointerPressed(eventArgs));
        }
        finally
        {
            EndInputRouting();
        }
    }

    internal void ProcessPointerReleased(
        Point position,
        MouseButton button,
        KeyModifiers modifiers)
    {
        BeginInputRouting(button);
        try
        {
            var logicalPosition = UpdatePointerPosition(position);
            UpdateHover(logicalPosition);
            if (!IsScreenActive())
                return;

            var buttonIndex = GetButtonIndex(button);
            var target = _pressedTargets[buttonIndex];
            _pressedTargets[buttonIndex] = null;
            if (button == MouseButton.Left)
            {
                var pressedControls = _leftPressedControls;
                _leftPressedControls = null;
                var errors = new List<Exception>();
                ClearPressedControls(pressedControls, errors);
                ThrowInputErrors(errors);
            }

            if (target is null || !IsActive(target))
                return;

            var path = BuildPathForNode(target, logicalPosition);
            if (path.Count == 0)
                return;

            var args = new UiPointerButtonEventArgs(
                target,
                logicalPosition,
                button,
                modifiers,
                path);
            Bubble(
                path,
                args,
                static (node, eventArgs) => node.RaisePointerReleased(eventArgs));

            if (!IsScreenActive() || !IsActive(target))
                return;

            var currentPath = BuildHitPath(logicalPosition);
            if (currentPath.Count == 0 || !ReferenceEquals(currentPath[^1].Node, target))
                return;

            var pointerClickedArgs = new UiPointerButtonEventArgs(
                target,
                logicalPosition,
                button,
                modifiers,
                currentPath);
            Bubble(
                currentPath,
                pointerClickedArgs,
                static (node, eventArgs) => node.RaisePointerClicked(eventArgs));
        }
        finally
        {
            EndInputRouting();
        }
    }

    internal void ProcessPointerWheel(Point position, double deltaX, double deltaY)
    {
        BeginInputRouting(deltaX, deltaY);
        try
        {
            var logicalPosition = UpdatePointerPosition(position);
            UpdateHover(logicalPosition);
            if (!IsScreenActive() || _hoverPath.Count == 0)
                return;

            var path = _hoverPath.ToArray();
            var args = new UiPointerWheelEventArgs(
                path[^1].Node,
                logicalPosition,
                deltaX,
                deltaY,
                path);
            Bubble(
                path,
                args,
                static (node, eventArgs) => node.RaisePointerWheel(eventArgs));
        }
        finally
        {
            EndInputRouting();
        }
    }

    internal void ProcessKeyDown(Key key, KeyModifiers modifiers)
    {
        ProcessKey(key, modifiers, static (node, eventArgs) => node.RaiseKeyDown(eventArgs));
    }

    internal void ProcessKeyUp(Key key, KeyModifiers modifiers)
    {
        ProcessKey(key, modifiers, static (node, eventArgs) => node.RaiseKeyUp(eventArgs));
    }

    internal void ProcessFocusChanged(bool focused)
    {
        BeginInputRouting();
        try
        {
            if (focused)
                return;

            var snapshot = CommitInputStateForClose();
            if (snapshot is not null)
                NotifyInputStateLoss(snapshot);
        }
        finally
        {
            EndInputRouting();
        }
    }

    internal InputStateCleanupSnapshot? CommitInputStateAfterTreeChange()
    {
        return CommitInputStateCore(clearAll: false);
    }

    internal void CommitAndNotifyInputStateAfterNodeDisabled(UiNode node)
    {
        if (!ReferenceEquals(node.Screen, this) || node.IsEnabled || !IsScreenActive())
            return;

        if (!BeginRuntimeOperationIfOpen())
            return;

        try
        {
            var snapshot = CommitInputStateCore(clearAll: false);
            if (snapshot is not null)
                NotifyInputStateLoss(snapshot);
        }
        finally
        {
            EndRuntimeOperation();
        }
    }

    internal void NotifyInputStateLoss(InputStateCleanupSnapshot snapshot)
    {
        var errors = new List<Exception>();
        if (snapshot.FocusedNode is { } focusedNode)
        {
            try
            {
                focusedNode.SetFocused(false);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

        for (var index = snapshot.PressedControls.Length - 1; index >= 0; index--)
        {
            try
            {
                snapshot.PressedControls[index].SetPressed(false);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

        for (var index = snapshot.ExitedNodes.Length - 1; index >= 0; index--)
        {
            try
            {
                snapshot.ExitedNodes[index].SetHovered(false);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

        if (snapshot.FocusedNode is { } focused)
        {
            var focusArgs = new UiFocusChangedEventArgs(focused, null);
            var ownsFocusGuard = !_isChangingFocus;
            if (ownsFocusGuard)
                _isChangingFocus = true;
            try
            {
                focused.RaiseLostFocus(focusArgs);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
            finally
            {
                if (ownsFocusGuard)
                    _isChangingFocus = false;
            }
        }

        if (snapshot.ExitedNodes.Length != 0)
        {
            var path = snapshot.HoverPath;
            var args = new UiPointerEventArgs(snapshot.ExitedNodes[^1], snapshot.PointerPosition, path);
            for (var index = snapshot.ExitedNodes.Length - 1; index >= 0; index--)
            {
                try
                {
                    snapshot.ExitedNodes[index].RaisePointerExited(args);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }
        }

        ThrowInputErrors(errors);
    }

    private InputStateCleanupSnapshot? CommitInputStateForClose()
    {
        var snapshot = CommitInputStateCore(clearAll: true);
        _pointerOutputPosition = Point.Zero;
        _pointerPosition = Point.Zero;
        _hasPointerPosition = false;
        return snapshot;
    }

    private InputStateCleanupSnapshot? CommitInputStateCore(bool clearAll)
    {
        var focused = FocusedNode;
        var lostFocus = focused is not null && (clearAll || !IsInputStateCurrent(focused));
        if (lostFocus)
            FocusedNode = null;

        for (var index = 0; index < _pressedTargets.Length; index++)
        {
            var target = _pressedTargets[index];
            if (target is not null && (clearAll || !IsInputStateCurrent(target)))
                _pressedTargets[index] = null;
        }

        Control[] pressedControls = [];
        if (_leftPressedControls is { } oldPressedControls)
        {
            var leftTarget = _pressedTargets[GetButtonIndex(MouseButton.Left)];
            if (clearAll || leftTarget is null)
            {
                pressedControls = oldPressedControls;
                _leftPressedControls = null;
            }
            else
            {
                pressedControls = oldPressedControls
                    .Where(control => !IsInputStateCurrent(control))
                    .ToArray();
                var retainedControls = oldPressedControls
                    .Where(IsInputStateCurrent)
                    .ToArray();
                _leftPressedControls = retainedControls.Length == 0 ? null : retainedControls;
            }
        }

        var oldHoverPath = _hoverPath.ToArray();
        var exitedNodes = oldHoverPath
            .Where(entry => clearAll || !IsInputStateCurrent(entry.Node))
            .Select(static entry => entry.Node)
            .ToArray();
        _hoverPath.Clear();
        if (!clearAll)
        {
            _hoverPath.AddRange(oldHoverPath.Where(entry => IsInputStateCurrent(entry.Node)));
        }

        if (!lostFocus && pressedControls.Length == 0 && exitedNodes.Length == 0)
            return null;

        return new InputStateCleanupSnapshot(
            lostFocus ? focused : null,
            pressedControls,
            oldHoverPath,
            exitedNodes,
            _pointerPosition);
    }

    private List<UiHitPathEntry> BuildHitPath(Point screenPoint)
    {
        var root = Root;
        if (root is null)
            return [];

        VerifyTreeAccess();
        var path = new List<UiHitPathEntry>();
        _ = root.TryBuildHitPath(
            new Point(
                screenPoint.X - root.LayoutBounds.X,
                screenPoint.Y - root.LayoutBounds.Y),
            path);
        return path;
    }

    private List<UiHitPathEntry> BuildPathForNode(UiNode node, Point screenPoint)
    {
        var root = Root;
        if (root is null || !IsActive(node))
            return [];

        var nodes = new List<UiNode>();
        for (var current = node; current is not null; current = current.Parent)
        {
            nodes.Add(current);
            if (ReferenceEquals(current, root))
                break;
        }

        if (!ReferenceEquals(nodes[^1], root))
            return [];

        nodes.Reverse();
        var path = new List<UiHitPathEntry>(nodes.Count);
        var localPoint = new Point(
            screenPoint.X - root.LayoutBounds.X,
            screenPoint.Y - root.LayoutBounds.Y);
        path.Add(new UiHitPathEntry(root, localPoint));
        for (var index = 1; index < nodes.Count; index++)
        {
            localPoint = new Point(
                localPoint.X - nodes[index].LayoutBounds.X,
                localPoint.Y - nodes[index].LayoutBounds.Y);
            path.Add(new UiHitPathEntry(nodes[index], localPoint));
        }

        return path;
    }

    private void ProcessKey(
        Key key,
        KeyModifiers modifiers,
        Action<UiNode, UiKeyEventArgs> raise)
    {
        BeginInputRouting();
        try
        {
            if (FocusedNode is not null && !CanFocus(FocusedNode))
                _ = ChangeFocus(null);
            if (!IsScreenActive() || FocusedNode is null)
                return;

            var path = BuildPathForNode(FocusedNode, _pointerPosition);
            if (path.Count == 0)
                return;

            var nodes = path.Select(static entry => entry.Node).ToArray();
            var args = new UiKeyEventArgs(FocusedNode, key, modifiers);
            for (var index = nodes.Length - 1; index >= 0; index--)
            {
                if (!IsScreenActive())
                    break;
                if (!IsActive(nodes[index]))
                    continue;
                raise(nodes[index], args);
                if (args.Handled)
                    break;
            }
        }
        finally
        {
            EndInputRouting();
        }
    }

    private void UpdateHover(Point screenPosition)
    {
        if (!IsScreenActive())
            return;

        var cleanupSnapshot = CommitInputStateCore(clearAll: false);
        if (cleanupSnapshot is not null)
            NotifyInputStateLoss(cleanupSnapshot);
        if (!IsScreenActive())
            return;

        _pointerPosition = screenPosition;
        var oldPath = _hoverPath.ToArray();
        var newPath = BuildHitPath(screenPosition).ToArray();
        _hoverPath.Clear();
        _hoverPath.AddRange(newPath);

        var commonLength = 0;
        while (commonLength < oldPath.Length &&
               commonLength < newPath.Length &&
               ReferenceEquals(oldPath[commonLength].Node, newPath[commonLength].Node))
        {
            commonLength++;
        }

        var errors = new List<Exception>();
        for (var index = oldPath.Length - 1; index >= commonLength; index--)
        {
            try
            {
                oldPath[index].Node.SetHovered(false);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

        for (var index = commonLength; index < newPath.Length; index++)
        {
            var node = newPath[index].Node;
            if (!CanSetHovered(node))
                continue;

            try
            {
                node.SetHovered(true);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

        if (oldPath.Length != 0)
        {
            var args = new UiPointerEventArgs(oldPath[^1].Node, screenPosition, oldPath);
            for (var index = oldPath.Length - 1; index >= commonLength; index--)
            {
                if (!IsScreenActive())
                    break;
                if (!IsActive(oldPath[index].Node))
                    continue;
                try
                {
                    oldPath[index].Node.RaisePointerExited(args);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }
        }

        if (newPath.Length != 0)
        {
            var args = new UiPointerEventArgs(newPath[^1].Node, screenPosition, newPath);
            for (var index = commonLength; index < newPath.Length; index++)
            {
                if (!IsScreenActive())
                    break;
                if (!IsActive(newPath[index].Node))
                    continue;
                try
                {
                    newPath[index].Node.RaisePointerEntered(args);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }
        }

        ThrowInputErrors(errors);
    }

    private Point UpdatePointerPosition(Point outputPosition)
    {
        var logicalPosition = ToLogicalPoint(outputPosition);
        _pointerOutputPosition = outputPosition;
        _hasPointerPosition = true;
        return logicalPosition;
    }

    private bool ChangeFocus(UiNode? next)
    {
        BeginRuntimeOperation();
        try
        {
            if (_isChangingFocus)
                throw new InvalidOperationException("A UI focus transition is already being notified.");
            if (ReferenceEquals(FocusedNode, next))
                return true;

            var old = FocusedNode;
            FocusedNode = next;
            var eventArgs = new UiFocusChangedEventArgs(old, next);
            var errors = new List<Exception>();
            _isChangingFocus = true;
            try
            {
                if (old is not null)
                {
                    try
                    {
                        old.SetFocused(false);
                    }
                    catch (Exception exception)
                    {
                        errors.Add(exception);
                    }
                }

                if (next is not null &&
                    IsScreenActive() &&
                    ReferenceEquals(FocusedNode, next) &&
                    ReferenceEquals(next.Screen, this) &&
                    CanFocus(next))
                {
                    try
                    {
                        next.SetFocused(true);
                    }
                    catch (Exception exception)
                    {
                        errors.Add(exception);
                    }
                }

                if (old is not null)
                {
                    try
                    {
                        old.RaiseLostFocus(eventArgs);
                    }
                    catch (Exception exception)
                    {
                        errors.Add(exception);
                    }
                }

                if (next is not null &&
                    IsScreenActive() &&
                    ReferenceEquals(FocusedNode, next) &&
                    IsActive(next))
                {
                    try
                    {
                        next.RaiseGotFocus(eventArgs);
                    }
                    catch (Exception exception)
                    {
                        errors.Add(exception);
                    }
                }
            }
            finally
            {
                _isChangingFocus = false;
            }

            ThrowInputErrors(errors);
            return true;
        }
        finally
        {
            EndRuntimeOperation();
        }
    }

    private bool CanFocus(UiNode node)
    {
        if (!IsActive(node) ||
            !node.IsEnabled ||
            !node.Focusable ||
            !node.IsArrangeValid ||
            node.Visibility != Visibility.Visible)
            return false;

        for (var ancestor = node.Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (!ancestor.IsEnabled || ancestor.Visibility != Visibility.Visible)
                return false;
            if (ReferenceEquals(ancestor, Root))
                return true;
        }

        return ReferenceEquals(node, Root);
    }

    private bool CanSetHovered(UiNode node) =>
        IsScreenActive() &&
        ReferenceEquals(node.Screen, this) &&
        ContainsNodeReference(_hoverPath, node) &&
        IsInputStateCurrent(node);

    private bool CanSetPressed(Control control)
    {
        if (!IsScreenActive() ||
            !ReferenceEquals(control.Screen, this) ||
            _leftPressedControls is not { } controls ||
            !ContainsControlReference(controls, control) ||
            !IsInputStateCurrent(control))
        {
            return false;
        }

        var target = _pressedTargets[GetButtonIndex(MouseButton.Left)];
        return target is not null && IsInputStateCurrent(target);
    }

    private void ProjectPressedDifference(
        IReadOnlyList<Control>? oldControls,
        IReadOnlyList<Control>? newControls,
        List<Exception> errors)
    {
        if (oldControls is not null)
        {
            for (var index = oldControls.Count - 1; index >= 0; index--)
            {
                var control = oldControls[index];
                if (newControls is not null && ContainsControlReference(newControls, control))
                    continue;

                try
                {
                    control.SetPressed(false);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }
        }

        if (newControls is null)
            return;

        for (var index = 0; index < newControls.Count; index++)
        {
            var control = newControls[index];
            if ((oldControls is not null && ContainsControlReference(oldControls, control)) ||
                !CanSetPressed(control))
            {
                continue;
            }

            try
            {
                control.SetPressed(true);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }
    }

    private static void ClearPressedControls(
        IReadOnlyList<Control>? controls,
        List<Exception> errors)
    {
        if (controls is null)
            return;

        for (var index = controls.Count - 1; index >= 0; index--)
        {
            try
            {
                controls[index].SetPressed(false);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }
    }

    private static Control[] GetControls(IReadOnlyList<UiHitPathEntry> path)
    {
        var controls = new List<Control>();
        foreach (var entry in path)
        {
            if (entry.Node is Control control)
                controls.Add(control);
        }

        return controls.ToArray();
    }

    private static bool ContainsControlReference(
        IReadOnlyList<Control> controls,
        Control candidate)
    {
        foreach (var control in controls)
        {
            if (ReferenceEquals(control, candidate))
                return true;
        }

        return false;
    }

    private bool IsInputStateCurrent(UiNode node)
    {
        if (!ReferenceEquals(node.Screen, this))
            return false;

        for (var current = node; current is not null; current = current.Parent)
        {
            if (!current.IsEnabled)
                return false;
            if (ReferenceEquals(current, Root))
                return true;
        }

        return false;
    }

    private static bool ContainsNodeReference(
        IReadOnlyList<UiHitPathEntry> path,
        UiNode node)
    {
        foreach (var entry in path)
        {
            if (ReferenceEquals(entry.Node, node))
                return true;
        }

        return false;
    }

    private void Bubble<TEventArgs>(
        IReadOnlyList<UiHitPathEntry> path,
        TEventArgs eventArgs,
        Action<UiNode, TEventArgs> raise)
        where TEventArgs : UiInputEventArgs
    {
        for (var index = path.Count - 1; index >= 0; index--)
        {
            if (!IsScreenActive())
                break;
            if (!IsActive(path[index].Node))
                continue;
            raise(path[index].Node, eventArgs);
            if (eventArgs.Handled)
                break;
        }
    }

    private bool IsScreenActive()
    {
        lock (_stateSync)
            return _ownerThreadId is not null && _isInteractionActive && !_isClosing;
    }

    private bool IsActive(UiNode node) =>
        IsScreenActive() && ReferenceEquals(node.Screen, this);

    private void BeginInputRouting()
    {
        VerifyCanBeginInputRouting();
        StartInputRouting();
    }

    private void BeginInputRouting(MouseButton button)
    {
        VerifyCanBeginInputRouting();
        VerifyMouseButton(button);
        StartInputRouting();
    }

    private void BeginInputRouting(double deltaX, double deltaY)
    {
        VerifyCanBeginInputRouting();
        VerifyWheelDelta(deltaX, nameof(deltaX));
        VerifyWheelDelta(deltaY, nameof(deltaY));
        StartInputRouting();
    }

    private void VerifyCanBeginInputRouting()
    {
        if (!IsScreenActive())
        {
            throw new InvalidOperationException("The UI screen is not active.");
        }

        VerifyOwnerThread();
        if (IsUpdatingLayout)
            throw new InvalidOperationException("The UI screen cannot route input during layout.");
        if (_isTransitioning)
            throw new InvalidOperationException("The UI screen cannot route input during a lifecycle transition.");
        if (_isRoutingInput)
            throw new InvalidOperationException("The UI screen is already routing input.");
    }

    private void StartInputRouting()
    {
        BeginRuntimeOperation();
        _isRoutingInput = true;
    }

    private void EndInputRouting()
    {
        _isRoutingInput = false;
        EndRuntimeOperation();
    }

    private static int GetButtonIndex(MouseButton button) =>
        (int)button - (int)MouseButton.Left;

    private static void VerifyMouseButton(MouseButton button)
    {
        if (button is < MouseButton.Left or > MouseButton.Button12)
            throw new ArgumentOutOfRangeException(nameof(button));
    }

    private static void VerifyWheelDelta(double value, string parameterName)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(parameterName, "A wheel delta must be finite.");
    }

    private static void ThrowInputErrors(List<Exception> errors)
    {
        if (errors.Count == 1)
            ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1)
            throw new AggregateException(errors);
    }
}

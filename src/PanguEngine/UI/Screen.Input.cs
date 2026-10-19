using PanguEngine.Input;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Input;

namespace PanguEngine.UI;

public partial class Screen
{
    private readonly List<HitPathEntry> _hoverPath = [];
    private UIElement[] _focusWithinPath = [];

    private readonly UIElement?[] _pressedTargets =
        new UIElement?[(int)MouseButton.Button12 - (int)MouseButton.Left + 1];

    private Control[]? _leftPressedControls;
    private Point _pointerOutputPosition;
    private Point _pointerPosition;
    private bool _hasPointerPosition;
    private bool _isChangingFocus;
    private bool _isRoutingInput;

    internal sealed class InputStateCleanupSnapshot
    {
        internal InputStateCleanupSnapshot(
            UIElement? focusedNode,
            UIElement[] focusWithinPath,
            Control[] pressedControls,
            HitPathEntry[] hoverPath,
            UIElement[] exitedNodes,
            Point pointerPosition)
        {
            FocusedNode = focusedNode;
            FocusWithinPath = focusWithinPath;
            PressedControls = pressedControls;
            HoverPath = hoverPath;
            ExitedNodes = exitedNodes;
            PointerPosition = pointerPosition;
        }

        internal UIElement? FocusedNode { get; }
        internal UIElement[] FocusWithinPath { get; }
        internal Control[] PressedControls { get; }
        internal HitPathEntry[] HoverPath { get; }
        internal UIElement[] ExitedNodes { get; }
        internal Point PointerPosition { get; }
    }

    /// <summary>
    /// Gets the element that currently owns keyboard focus.
    /// </summary>
    public UIElement? FocusedNode { get; private set; }

    /// <summary>
    /// Finds the frontmost deepest element at a point in screen logical coordinates.
    /// </summary>
    /// <param name="screenPoint">The point in screen logical coordinates.</param>
    /// <returns>The deepest hit element, or null when the root subtree is not hit.</returns>
    public UIElement? HitTest(Point screenPoint)
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

    internal bool TryFocus(UIElement node)
    {
        if (!IsScreenActive())
            return false;

        VerifyOwnerThread();
        if (!CanFocus(node))
            return false;

        var target = node.GetFocusTarget();
        return CanFocus(target) && ChangeFocus(target);
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

    /// <summary>
    /// Updates pointer hover state and routes a pointer movement event.
    /// </summary>
    /// <param name="position">The pointer position in output coordinates.</param>
    /// <returns>Whether the routed movement event was handled.</returns>
    /// <remarks>
    /// Call on the active screen's owner thread, outside lifecycle transitions, layout, and other input routing.
    /// </remarks>
    public bool ProcessPointerMoved(Point position)
    {
        BeginInputRouting();
        try
        {
            var logicalPosition = UpdatePointerPosition(position);
            UpdateHover(logicalPosition);

            if (!IsScreenActive())
                return false;

            HitPathEntry[] path;
            var pressedTarget = _pressedTargets[GetButtonIndex(MouseButton.Left)];
            if (pressedTarget is not null && ReferenceEquals(pressedTarget.Screen, this))
                path = BuildPathForNode(pressedTarget, logicalPosition).ToArray();
            else
                path = _hoverPath.ToArray();
            if (path.Length == 0)
                return false;

            var args = new PointerEventArgs(path[^1].Node, logicalPosition, path);
            Bubble(
                path,
                args,
                static (node, eventArgs) => node.RaisePointerMoved(eventArgs));
            return args.Handled;
        }
        finally
        {
            EndInputRouting();
        }
    }

    /// <summary>
    /// Updates pointer and focus state and routes a pointer press event.
    /// </summary>
    /// <param name="position">The pointer position in output coordinates.</param>
    /// <param name="button">The button that was pressed.</param>
    /// <param name="modifiers">The modifiers held when the button was pressed.</param>
    /// <returns>Whether the routed press event was handled.</returns>
    /// <remarks>
    /// Call on the active screen's owner thread, outside lifecycle transitions, layout, and other input routing.
    /// </remarks>
    public bool ProcessPointerPressed(
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
                return false;

            var path = _hoverPath.ToArray();
            var index = GetButtonIndex(button);
            var target = path.Length == 0 ? null : path[^1].Node;
            _pressedTargets[index] = target;
            if (button == MouseButton.Left)
            {
                var oldPressedControls = _leftPressedControls;
                var controls = GetControls(path);
                var newPressedControls = controls.Length == 0 ? null : controls;
                _leftPressedControls = newPressedControls;

                ProjectPressedDifference(oldPressedControls, newPressedControls);
            }

            if (!IsScreenActive())
                return false;

            UIElement? focusCandidate = null;
            for (var pathIndex = path.Length - 1; pathIndex >= 0; pathIndex--)
            {
                if (CanFocus(path[pathIndex].Node))
                {
                    var targetNode = path[pathIndex].Node.GetFocusTarget();
                    if (CanFocus(targetNode))
                    {
                        focusCandidate = targetNode;
                        break;
                    }
                }
            }

            _ = ChangeFocus(focusCandidate);
            if (!IsScreenActive() || path.Length == 0)
                return false;

            var args = new PointerButtonEventArgs(
                path[^1].Node,
                logicalPosition,
                button,
                modifiers,
                path);
            Bubble(
                path,
                args,
                static (node, eventArgs) => node.RaisePointerPressed(eventArgs));
            return args.Handled;
        }
        finally
        {
            EndInputRouting();
        }
    }

    /// <summary>
    /// Routes a pointer release and, when applicable, a click to the corresponding press target.
    /// </summary>
    /// <param name="position">The pointer position in output coordinates.</param>
    /// <param name="button">The button that was released.</param>
    /// <param name="modifiers">The modifiers held when the button was released.</param>
    /// <returns>Whether either the routed release or click event was handled.</returns>
    /// <remarks>
    /// Call on the active screen's owner thread, outside lifecycle transitions, layout, and other input routing.
    /// </remarks>
    public bool ProcessPointerReleased(
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
                return false;

            var buttonIndex = GetButtonIndex(button);
            var target = _pressedTargets[buttonIndex];
            _pressedTargets[buttonIndex] = null;
            if (button == MouseButton.Left)
            {
                var pressedControls = _leftPressedControls;
                _leftPressedControls = null;
                ClearPressedControls(pressedControls);
            }

            if (target is null || !IsActive(target))
                return false;

            var path = BuildPathForNode(target, logicalPosition);
            if (path.Count == 0)
                return false;

            var args = new PointerButtonEventArgs(
                target,
                logicalPosition,
                button,
                modifiers,
                path);
            Bubble(
                path,
                args,
                static (node, eventArgs) => node.RaisePointerReleased(eventArgs));
            var handled = args.Handled;

            if (!IsActive(target))
                return handled;

            var currentPath = BuildHitPath(logicalPosition);
            if (currentPath.Count == 0 || !ReferenceEquals(currentPath[^1].Node, target))
                return handled;

            var pointerClickedArgs = new PointerButtonEventArgs(
                target,
                logicalPosition,
                button,
                modifiers,
                currentPath);
            Bubble(
                currentPath,
                pointerClickedArgs,
                static (node, eventArgs) => node.RaisePointerClicked(eventArgs));
            return handled || pointerClickedArgs.Handled;
        }
        finally
        {
            EndInputRouting();
        }
    }

    /// <summary>
    /// Updates pointer hover state and routes a pointer wheel event.
    /// </summary>
    /// <param name="position">The pointer position in output coordinates.</param>
    /// <param name="deltaX">The horizontal wheel delta.</param>
    /// <param name="deltaY">The vertical wheel delta.</param>
    /// <param name="modifiers">The modifiers held when the wheel event occurred.</param>
    /// <returns>Whether the routed wheel event was handled.</returns>
    /// <remarks>
    /// Call on the active screen's owner thread, outside lifecycle transitions, layout, and other input routing.
    /// </remarks>
    public bool ProcessPointerWheel(
        Point position,
        double deltaX,
        double deltaY,
        KeyModifiers modifiers = KeyModifiers.None)
    {
        BeginInputRouting(deltaX, deltaY);
        try
        {
            var logicalPosition = UpdatePointerPosition(position);
            UpdateHover(logicalPosition);
            if (!IsScreenActive() || _hoverPath.Count == 0)
                return false;

            var path = _hoverPath.ToArray();
            var args = new PointerWheelEventArgs(
                path[^1].Node,
                logicalPosition,
                deltaX,
                deltaY,
                modifiers,
                path);
            Bubble(
                path,
                args,
                static (node, eventArgs) => node.RaisePointerWheel(eventArgs));
            return args.Handled;
        }
        finally
        {
            EndInputRouting();
        }
    }

    /// <summary>
    /// Routes a key press event from the focused element.
    /// </summary>
    /// <param name="key">The key that was pressed.</param>
    /// <param name="modifiers">The modifiers held when the key was pressed.</param>
    /// <param name="isRepeat">Whether this is a repeated key press.</param>
    /// <returns>Whether the routed key event was handled.</returns>
    /// <remarks>
    /// Call on the active screen's owner thread, outside lifecycle transitions, layout, and other input routing.
    /// </remarks>
    public bool ProcessKeyDown(Key key, KeyModifiers modifiers, bool isRepeat = false)
    {
        return ProcessKey(key, modifiers, isRepeat, static (node, eventArgs) => node.RaiseKeyDown(eventArgs));
    }

    /// <summary>
    /// Routes a key release event from the focused element.
    /// </summary>
    /// <param name="key">The key that was released.</param>
    /// <param name="modifiers">The modifiers held when the key was released.</param>
    /// <param name="isRepeat">Whether the input source marked this event as a repeat.</param>
    /// <returns>Whether the routed key event was handled.</returns>
    /// <remarks>
    /// Call on the active screen's owner thread, outside lifecycle transitions, layout, and other input routing.
    /// </remarks>
    public bool ProcessKeyUp(Key key, KeyModifiers modifiers, bool isRepeat = false)
    {
        return ProcessKey(key, modifiers, isRepeat, static (node, eventArgs) => node.RaiseKeyUp(eventArgs));
    }

    /// <summary>
    /// Routes committed text input from the focused element.
    /// </summary>
    /// <param name="text">The text supplied by the input source.</param>
    /// <remarks>
    /// Call on the active screen's owner thread, outside lifecycle transitions, layout, and other input routing.
    /// </remarks>
    public void ProcessTextInput(string text)
    {
        _ = ProcessFocusedInput(
            source => new TextInputEventArgs(source, text),
            static (node, eventArgs) => node.RaiseTextInput(eventArgs));
    }

    /// <summary>
    /// Processes a host focus change, clearing interaction state and canceling pending pointer input on focus loss.
    /// </summary>
    /// <param name="focused">Whether the host has focus.</param>
    /// <remarks>
    /// Call on the active screen's owner thread, outside lifecycle transitions, layout, and other input routing.
    /// Regaining host focus does not restore previous element focus or pointer state.
    /// </remarks>
    public void ProcessFocusChanged(bool focused)
    {
        BeginInputRouting();
        try
        {
            if (focused)
                return;

            CancelPendingPointerInput();
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

    internal void CommitAndNotifyInputStateAfterNodeUnavailable(UIElement node)
    {
        if (!ReferenceEquals(node.Screen, this) ||
            (node.IsEnabled && node.Visibility == Visibility.Visible) ||
            !IsScreenActive())
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
        var ownsFocusGuard = snapshot.FocusedNode is not null && !_isChangingFocus;
        if (ownsFocusGuard)
            _isChangingFocus = true;
        try
        {
            ClearFocusWithin(snapshot.FocusWithinPath, 0);
            if (snapshot.FocusedNode is { } focusedNode)
                ClearFocusedState(focusedNode);

            for (var index = snapshot.PressedControls.Length - 1; index >= 0; index--)
                snapshot.PressedControls[index].SetPressed(false);

            for (var index = snapshot.ExitedNodes.Length - 1; index >= 0; index--)
                snapshot.ExitedNodes[index].SetHovered(false);

            if (snapshot.FocusedNode is { } focused)
                focused.RaiseLostFocus(new FocusChangedEventArgs(focused, null));

            if (snapshot.ExitedNodes.Length != 0)
            {
                var args = new PointerEventArgs(snapshot.ExitedNodes[^1], snapshot.PointerPosition, snapshot.HoverPath);
                for (var index = snapshot.ExitedNodes.Length - 1; index >= 0; index--)
                    snapshot.ExitedNodes[index].RaisePointerExited(args);
            }
        }
        finally
        {
            if (ownsFocusGuard)
                _isChangingFocus = false;
        }
    }

    private void CancelPendingPointerInput()
    {
        var pressedTargets = _pressedTargets.ToArray();
        var pointerPosition = _pointerPosition;
        var snapshot = CommitInputStateForClose();
        if (snapshot is not null)
            NotifyInputStateLoss(snapshot);

        for (var index = 0; index < pressedTargets.Length; index++)
        {
            var target = pressedTargets[index];
            if (target is null || !IsInputStateCurrent(target))
                continue;

            var path = BuildPathForNode(target, pointerPosition);
            if (path.Count == 0)
                continue;

            var args = new PointerButtonEventArgs(
                target,
                pointerPosition,
                (MouseButton)((int)MouseButton.Left + index),
                KeyModifiers.None,
                path);
            Bubble(
                path,
                args,
                static (node, eventArgs) => node.RaisePointerCanceled(eventArgs));
        }
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
        var focusWithinPath = lostFocus ? _focusWithinPath : [];
        if (lostFocus)
        {
            FocusedNode = null;
            _focusWithinPath = [];
        }

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
            focusWithinPath,
            pressedControls,
            oldHoverPath,
            exitedNodes,
            _pointerPosition);
    }

    private List<HitPathEntry> BuildHitPath(Point screenPoint)
    {
        var root = Root;
        if (root is null)
            return [];

        VerifyTreeAccess();
        var path = new List<HitPathEntry>();
        _ = root.TryBuildHitPath(
            new Point(
                screenPoint.X - root.LayoutBounds.X,
                screenPoint.Y - root.LayoutBounds.Y),
            path);
        return path;
    }

    private List<HitPathEntry> BuildPathForNode(UIElement node, Point screenPoint)
    {
        var root = Root;
        if (root is null || !ReferenceEquals(node.Screen, this))
            return [];

        var nodes = new List<UIElement>();
        for (var current = node; current is not null; current = current.Parent)
        {
            nodes.Add(current);
            if (ReferenceEquals(current, root))
                break;
        }

        if (!ReferenceEquals(nodes[^1], root))
            return [];

        nodes.Reverse();
        var path = new List<HitPathEntry>(nodes.Count);
        var localPoint = new Point(
            screenPoint.X - root.LayoutBounds.X,
            screenPoint.Y - root.LayoutBounds.Y);
        path.Add(new HitPathEntry(root, localPoint));
        for (var index = 1; index < nodes.Count; index++)
        {
            localPoint = new Point(
                localPoint.X - nodes[index].LayoutBounds.X,
                localPoint.Y - nodes[index].LayoutBounds.Y);
            path.Add(new HitPathEntry(nodes[index], localPoint));
        }

        return path;
    }

    private bool ProcessKey(
        Key key,
        KeyModifiers modifiers,
        bool isRepeat,
        Action<UIElement, KeyEventArgs> raise)
    {
        return ProcessFocusedInput(
            source => new KeyEventArgs(source, key, modifiers, isRepeat),
            raise);
    }

    private bool ProcessFocusedInput<TEventArgs>(
        Func<UIElement, TEventArgs> createEventArgs,
        Action<UIElement, TEventArgs> raise)
        where TEventArgs : InputEventArgs
    {
        BeginInputRouting();
        try
        {
            if (FocusedNode is not null && !CanFocus(FocusedNode))
                _ = ChangeFocus(null);
            if (!IsScreenActive() || FocusedNode is null)
                return false;

            var path = BuildPathForNode(FocusedNode, _pointerPosition);
            if (path.Count == 0)
                return false;

            var args = createEventArgs(FocusedNode);
            Bubble(path, args, raise);
            return args.Handled;
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

        for (var index = oldPath.Length - 1; index >= commonLength; index--)
            oldPath[index].Node.SetHovered(false);

        for (var index = commonLength; index < newPath.Length; index++)
        {
            var node = newPath[index].Node;
            if (!CanSetHovered(node))
                continue;

            node.SetHovered(true);
        }

        if (oldPath.Length != 0)
        {
            var args = new PointerEventArgs(oldPath[^1].Node, screenPosition, oldPath);
            for (var index = oldPath.Length - 1; index >= commonLength; index--)
            {
                if (!IsScreenActive())
                    return;
                if (!ReferenceEquals(oldPath[index].Node.Screen, this))
                    continue;
                oldPath[index].Node.RaisePointerExited(args);
            }
        }

        if (newPath.Length != 0)
        {
            var args = new PointerEventArgs(newPath[^1].Node, screenPosition, newPath);
            for (var index = commonLength; index < newPath.Length; index++)
            {
                if (!IsScreenActive())
                    return;
                if (!ReferenceEquals(newPath[index].Node.Screen, this))
                    continue;
                newPath[index].Node.RaisePointerEntered(args);
            }
        }
    }

    private Point UpdatePointerPosition(Point outputPosition)
    {
        var logicalPosition = ToLogicalPoint(outputPosition);
        _pointerOutputPosition = outputPosition;
        _hasPointerPosition = true;
        return logicalPosition;
    }

    private bool ChangeFocus(UIElement? next)
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
            var eventArgs = new FocusChangedEventArgs(old, next);
            _isChangingFocus = true;
            try
            {
                UpdateFocusWithin(next);
                if (old is not null)
                    ClearFocusedState(old);

                if (next is not null &&
                    IsScreenActive() &&
                    ReferenceEquals(FocusedNode, next) &&
                    ReferenceEquals(next.Screen, this) &&
                    CanFocus(next))
                {
                    next.SetFocused(true);
                }

                old?.RaiseLostFocus(eventArgs);

                if (next is not null &&
                    IsScreenActive() &&
                    ReferenceEquals(FocusedNode, next) &&
                    IsActive(next))
                {
                    next.RaiseGotFocus(eventArgs);
                }
            }
            finally
            {
                _isChangingFocus = false;
            }

            return true;
        }
        finally
        {
            EndRuntimeOperation();
        }
    }

    private void UpdateFocusWithin(UIElement? focused)
    {
        var nodes = new List<UIElement>();
        for (var current = focused; current is not null; current = current.Parent)
            nodes.Add(current);
        nodes.Reverse();

        var oldPath = _focusWithinPath;
        var newPath = nodes.ToArray();
        _focusWithinPath = newPath;
        var commonLength = 0;
        while (commonLength < oldPath.Length &&
               commonLength < newPath.Length &&
               ReferenceEquals(oldPath[commonLength], newPath[commonLength]))
        {
            commonLength++;
        }

        ClearFocusWithin(oldPath, commonLength);
        for (var index = commonLength; index < newPath.Length; index++)
        {
            if (!ReferenceEquals(_focusWithinPath, newPath))
                return;

            newPath[index].SetFocusWithin(true);
        }
    }

    private static void ClearFocusWithin(UIElement[] path, int startIndex)
    {
        for (var index = path.Length - 1; index >= startIndex; index--)
        {
            var node = path[index];
            if (node.Screen is { } screen &&
                screen._focusWithinPath.Any(current => ReferenceEquals(current, node)))
            {
                continue;
            }

            node.SetFocusWithin(false);
        }
    }

    private static void ClearFocusedState(UIElement node)
    {
        if (node.Screen is { } screen && ReferenceEquals(screen.FocusedNode, node))
            return;

        node.SetFocused(false);
    }

    private bool CanFocus(UIElement node)
    {
        if (!IsActive(node) ||
            !node.IsEnabled ||
            !node.Focusable ||
            node.Visibility != Visibility.Visible)
        {
            return false;
        }

        for (var ancestor = node.Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (!ancestor.IsEnabled || ancestor.Visibility != Visibility.Visible)
                return false;
            if (ReferenceEquals(ancestor, Root))
                return true;
        }

        return ReferenceEquals(node, Root);
    }

    private bool CanSetHovered(UIElement node) =>
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
        Control[]? oldControls,
        Control[]? newControls)
    {
        if (oldControls is not null)
        {
            for (var index = oldControls.Length - 1; index >= 0; index--)
            {
                var control = oldControls[index];
                if (newControls is not null && ContainsControlReference(newControls, control))
                    continue;

                control.SetPressed(false);
            }
        }

        if (newControls is null)
            return;

        foreach (var control in newControls)
        {
            if ((oldControls is not null && ContainsControlReference(oldControls, control)) ||
                !CanSetPressed(control))
            {
                continue;
            }

            control.SetPressed(true);
        }
    }

    private static void ClearPressedControls(Control[]? controls)
    {
        if (controls is null)
            return;

        for (var index = controls.Length - 1; index >= 0; index--)
            controls[index].SetPressed(false);
    }

    private static Control[] GetControls(IReadOnlyList<HitPathEntry> path)
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
        Control[] controls,
        Control candidate)
    {
        foreach (var control in controls)
        {
            if (ReferenceEquals(control, candidate))
                return true;
        }

        return false;
    }

    private bool IsInputStateCurrent(UIElement node)
    {
        if (!ReferenceEquals(node.Screen, this))
            return false;

        for (var current = node; current is not null; current = current.Parent)
        {
            if (!current.IsEnabled || current.Visibility != Visibility.Visible)
                return false;
            if (ReferenceEquals(current, Root))
                return true;
        }

        return false;
    }

    private static bool ContainsNodeReference(
        IReadOnlyList<HitPathEntry> path,
        UIElement node)
    {
        foreach (var entry in path)
        {
            if (ReferenceEquals(entry.Node, node))
                return true;
        }

        return false;
    }

    private void Bubble<TEventArgs>(
        IReadOnlyList<HitPathEntry> path,
        TEventArgs eventArgs,
        Action<UIElement, TEventArgs> raise)
        where TEventArgs : InputEventArgs
    {
        for (var index = path.Count - 1; index >= 0; index--)
        {
            if (!ReferenceEquals(path[index].Node.Screen, this))
                continue;
            raise(path[index].Node, eventArgs);
            if (eventArgs.Handled)
                break;
        }
    }

    /// <summary>Gets whether this screen is open and active.</summary>
    /// <returns>Whether the screen can process updates and input.</returns>
    private protected bool IsScreenActive()
    {
        lock (_stateSync)
            return _ownerThreadId is not null && _isInteractionActive && !_isClosing;
    }

    private bool IsActive(UIElement node) =>
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
}

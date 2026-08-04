using PanguEngine.Input;
using System.Runtime.ExceptionServices;

namespace PanguEngine.Client.UI;

public partial class Screen
{
    private readonly List<UiHitPathEntry> _hoverPath = [];
    private readonly UiNode?[] _pressedTargets =
        new UiNode?[(int)MouseButton.Button12 - (int)MouseButton.Left + 1];
    private readonly HashSet<ulong> _focusChangeVersions = [];
    private Point _pointerPosition;
    private ulong _activationVersion;
    private bool _hasPointerPosition;
    private bool _isRoutingInput;

    /// <summary>
    /// Gets the node that currently owns keyboard focus.
    /// </summary>
    public UiNode? FocusedNode { get; private set; }

    /// <summary>
    /// Finds the frontmost deepest node at a point in screen coordinates.
    /// </summary>
    /// <param name="screenPoint">The point in screen coordinates.</param>
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

        Root.ActiveDispatcher!.VerifyAccess();
        _ = ChangeFocus(null);
    }

    internal bool TryFocus(UiNode node)
    {
        if (!IsScreenActive())
            return false;

        Root.ActiveDispatcher!.VerifyAccess();
        if (!CanFocus(node))
            return false;

        return ChangeFocus(node);
    }

    internal void ResetInputStateForOpening()
    {
        _activationVersion++;
        _hoverPath.Clear();
        Array.Clear(_pressedTargets, 0, _pressedTargets.Length);
        _pointerPosition = Point.Zero;
        _hasPointerPosition = false;
        FocusedNode = null;
    }

    internal void RefreshPointerAfterLayout()
    {
        if (!IsScreenActive() || !_hasPointerPosition)
            return;

        Root.ActiveDispatcher!.VerifyAccess();
        var activationVersion = BeginInputRouting();
        try
        {
            UpdateHover(_pointerPosition, activationVersion);
        }
        finally
        {
            EndInputRouting();
        }
    }

    internal void ProcessPointerMoved(Point position)
    {
        var activationVersion = BeginInputRouting();
        try
        {
            UpdateHover(position, activationVersion);
            if (!IsCurrentActivation(activationVersion) || _hoverPath.Count == 0)
                return;

            var path = _hoverPath.ToArray();
            var args = new UiPointerEventArgs(path[^1].Node, position, path);
            Bubble(
                path,
                args,
                activationVersion,
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
        var activationVersion = BeginInputRouting();
        try
        {
            UpdateHover(position, activationVersion);
            if (!IsCurrentActivation(activationVersion))
                return;

            var path = _hoverPath.ToArray();
            var index = GetButtonIndex(button);
            _pressedTargets[index] = path.Length == 0 ? null : path[^1].Node;

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
            if (!IsCurrentActivation(activationVersion) || path.Length == 0)
                return;

            var args = new UiPointerButtonEventArgs(
                path[^1].Node,
                position,
                button,
                modifiers,
                path);
            Bubble(
                path,
                args,
                activationVersion,
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
        var activationVersion = BeginInputRouting();
        try
        {
            UpdateHover(position, activationVersion);
            if (!IsCurrentActivation(activationVersion))
                return;

            var target = _pressedTargets[GetButtonIndex(button)];
            _pressedTargets[GetButtonIndex(button)] = null;
            if (target is null || !IsActive(target))
                return;

            var path = BuildPathForNode(target, position);
            if (path.Count == 0)
                return;

            var args = new UiPointerButtonEventArgs(
                target,
                position,
                button,
                modifiers,
                path);
            Bubble(
                path,
                args,
                activationVersion,
                static (node, eventArgs) => node.RaisePointerReleased(eventArgs));

            if (!IsCurrentActivation(activationVersion) || !IsActive(target))
                return;

            var currentPath = BuildHitPath(position);
            if (currentPath.Count == 0 || !ReferenceEquals(currentPath[^1].Node, target))
                return;

            var pointerClickedArgs = new UiPointerButtonEventArgs(
                target,
                position,
                button,
                modifiers,
                currentPath);
            Bubble(
                currentPath,
                pointerClickedArgs,
                activationVersion,
                static (node, eventArgs) => node.RaisePointerClicked(eventArgs));
        }
        finally
        {
            EndInputRouting();
        }
    }

    internal void ProcessPointerWheel(Point position, double deltaX, double deltaY)
    {
        var activationVersion = BeginInputRouting();
        try
        {
            UpdateHover(position, activationVersion);
            if (!IsCurrentActivation(activationVersion) || _hoverPath.Count == 0)
                return;

            var path = _hoverPath.ToArray();
            var args = new UiPointerWheelEventArgs(
                path[^1].Node,
                position,
                deltaX,
                deltaY,
                path);
            Bubble(
                path,
                args,
                activationVersion,
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

    internal void HandleSubtreesDetached(IReadOnlyCollection<UiNode> detachedNodes)
    {
        if (detachedNodes.Count == 0)
            return;

        var errors = new List<Exception>();
        var focused = FocusedNode;
        var lostFocus = focused is not null && detachedNodes.Contains(focused);
        if (lostFocus)
            FocusedNode = null;

        for (var index = 0; index < _pressedTargets.Length; index++)
        {
            if (_pressedTargets[index] is not null && detachedNodes.Contains(_pressedTargets[index]!))
                _pressedTargets[index] = null;
        }

        var oldHoverPath = _hoverPath.ToArray();
        var hoverIndex = Array.FindIndex(
            oldHoverPath,
            entry => detachedNodes.Contains(entry.Node));
        if (hoverIndex >= 0)
        {
            _hoverPath.Clear();
            _hoverPath.AddRange(oldHoverPath.AsSpan(0, hoverIndex).ToArray());
        }

        if (lostFocus)
        {
            var focusArgs = new UiFocusChangedEventArgs(focused, null);
            var focusVersion = _activationVersion;
            var addedFocusGuard = _focusChangeVersions.Add(focusVersion);
            try
            {
                focused!.RaiseLostFocus(focusArgs);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
            finally
            {
                if (addedFocusGuard)
                    _focusChangeVersions.Remove(focusVersion);
            }
        }

        if (hoverIndex >= 0 && oldHoverPath.Length != 0)
        {
            var path = oldHoverPath;
            var args = new UiPointerEventArgs(path[^1].Node, _pointerPosition, path);
            for (var index = path.Length - 1; index >= hoverIndex; index--)
            {
                var node = path[index].Node;
                try
                {
                    node.RaisePointerExited(args);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }
        }

        ThrowInputErrors(errors);
    }

    internal void ClearInputStateAfterClose()
    {
        _hoverPath.Clear();
        Array.Clear(_pressedTargets, 0, _pressedTargets.Length);
        _pointerPosition = Point.Zero;
        _hasPointerPosition = false;
        FocusedNode = null;
    }

    private List<UiHitPathEntry> BuildHitPath(Point screenPoint)
    {
        Root.ActiveDispatcher?.VerifyAccess();
        var rootPoint = new Point(
            screenPoint.X - Root.LayoutBounds.X,
            screenPoint.Y - Root.LayoutBounds.Y);
        var path = new List<UiHitPathEntry>();
        _ = Root.TryBuildHitPath(rootPoint, path);
        return path;
    }

    private List<UiHitPathEntry> BuildPathForNode(UiNode node, Point screenPoint)
    {
        if (!IsActive(node))
            return [];

        var nodes = new List<UiNode>();
        for (UiNode? current = node; current is not null; current = current.Parent)
        {
            nodes.Add(current);
            if (ReferenceEquals(current, Root))
                break;
        }

        if (!ReferenceEquals(nodes[^1], Root))
            return [];

        nodes.Reverse();
        var path = new List<UiHitPathEntry>(nodes.Count);
        var localPoint = new Point(
            screenPoint.X - Root.LayoutBounds.X,
            screenPoint.Y - Root.LayoutBounds.Y);
        path.Add(new UiHitPathEntry(Root, localPoint));
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
        var activationVersion = BeginInputRouting();
        try
        {
            if (FocusedNode is not null && !CanFocus(FocusedNode))
                _ = ChangeFocus(null);
            if (!IsCurrentActivation(activationVersion) || FocusedNode is null)
                return;

            var path = BuildPathForNode(FocusedNode, _pointerPosition);
            if (path.Count == 0)
                return;

            var nodes = path.Select(static entry => entry.Node).ToArray();
            var args = new UiKeyEventArgs(FocusedNode, key, modifiers);
            for (var index = nodes.Length - 1; index >= 0; index--)
            {
                if (!IsCurrentActivation(activationVersion))
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

    private void UpdateHover(Point screenPosition, ulong activationVersion)
    {
        if (!IsCurrentActivation(activationVersion))
            return;

        _pointerPosition = screenPosition;
        _hasPointerPosition = true;
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
        if (oldPath.Length != 0)
        {
            var args = new UiPointerEventArgs(oldPath[^1].Node, screenPosition, oldPath);
            for (var index = oldPath.Length - 1; index >= commonLength; index--)
            {
                if (!IsCurrentActivation(activationVersion))
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
                if (!IsCurrentActivation(activationVersion))
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

    private bool ChangeFocus(UiNode? next)
    {
        var activationVersion = _activationVersion;
        if (_focusChangeVersions.Contains(activationVersion))
            throw new InvalidOperationException("A UI focus transition is already being notified.");
        if (ReferenceEquals(FocusedNode, next))
            return true;

        var old = FocusedNode;
        FocusedNode = next;
        var eventArgs = new UiFocusChangedEventArgs(old, next);
        var errors = new List<Exception>();
        _focusChangeVersions.Add(activationVersion);
        try
        {
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
                IsCurrentActivation(activationVersion) &&
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
            _focusChangeVersions.Remove(activationVersion);
        }

        ThrowInputErrors(errors);
        return true;
    }

    private bool CanFocus(UiNode node)
    {
        if (!IsActive(node) || !node.Focusable || !node.IsArrangeValid || node.Visibility != Visibility.Visible)
            return false;

        for (UiNode? ancestor = node.Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ancestor.Visibility != Visibility.Visible)
                return false;
            if (ReferenceEquals(ancestor, Root))
                return true;
        }

        return ReferenceEquals(node, Root);
    }

    private void Bubble<TEventArgs>(
        IReadOnlyList<UiHitPathEntry> path,
        TEventArgs eventArgs,
        ulong activationVersion,
        Action<UiNode, TEventArgs> raise)
        where TEventArgs : UiInputEventArgs
    {
        for (var index = path.Count - 1; index >= 0; index--)
        {
            if (!IsCurrentActivation(activationVersion))
                break;
            if (!IsActive(path[index].Node))
                continue;
            raise(path[index].Node, eventArgs);
            if (eventArgs.Handled)
                break;
        }
    }

    private bool IsScreenActive() =>
        ReferenceEquals(Root.ActiveScreen, this) && Root.ActiveDispatcher is not null;

    private bool IsActive(UiNode node) =>
        ReferenceEquals(node.ActiveScreen, this);

    private bool IsCurrentActivation(ulong activationVersion) =>
        _activationVersion == activationVersion && IsScreenActive();

    private ulong BeginInputRouting()
    {
        var dispatcher = Root.ActiveDispatcher;
        if (!ReferenceEquals(Root.ActiveScreen, this) || dispatcher is null)
            throw new InvalidOperationException("The UI screen is not active.");

        dispatcher.VerifyAccess();
        if (_isRoutingInput)
            throw new InvalidOperationException("The UI screen is already routing input.");
        _isRoutingInput = true;
        return _activationVersion;
    }

    private void EndInputRouting() =>
        _isRoutingInput = false;

    private static int GetButtonIndex(MouseButton button) =>
        (int)button - (int)MouseButton.Left;

    internal static void VerifyMouseButton(MouseButton button)
    {
        if (button is < MouseButton.Left or > MouseButton.Button12)
            throw new ArgumentOutOfRangeException(nameof(button));
    }

    internal static void VerifyWheelDelta(double value, string parameterName)
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

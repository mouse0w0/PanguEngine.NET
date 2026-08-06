namespace PanguEngine.Client.UI;

public abstract partial class UiNode
{
    /// <summary>
    /// Identifies the <see cref="Focusable"/> property.
    /// </summary>
    public static readonly UiProperty<bool> FocusableProperty =
        UiProperty.Register<UiNode, bool>(
            nameof(Focusable),
            invalidation: UiPropertyInvalidation.Input);

    /// <summary>
    /// Identifies the <see cref="IsHitTestVisible"/> property.
    /// </summary>
    public static readonly UiProperty<bool> IsHitTestVisibleProperty =
        UiProperty.Register<UiNode, bool>(
            nameof(IsHitTestVisible),
            true,
            UiPropertyInvalidation.Input);

    /// <summary>
    /// Gets or sets whether this node can receive keyboard focus.
    /// </summary>
    public bool Focusable
    {
        get => GetValue(FocusableProperty);
        set => SetValue(FocusableProperty, value);
    }

    /// <summary>
    /// Gets or sets whether this node and its subtree participate in hit testing.
    /// </summary>
    public bool IsHitTestVisible
    {
        get => GetValue(IsHitTestVisibleProperty);
        set => SetValue(IsHitTestVisibleProperty, value);
    }

    /// <summary>
    /// Occurs when the pointer enters this node's hit path.
    /// </summary>
    public event EventHandler<UiPointerEventArgs>? PointerEntered;

    /// <summary>
    /// Occurs when the pointer leaves this node's hit path.
    /// </summary>
    public event EventHandler<UiPointerEventArgs>? PointerExited;

    /// <summary>
    /// Occurs when the pointer moves through this node's hit path.
    /// </summary>
    public event EventHandler<UiPointerEventArgs>? PointerMoved;

    /// <summary>
    /// Occurs when a pointer button is pressed on this node's hit path.
    /// </summary>
    public event EventHandler<UiPointerButtonEventArgs>? PointerPressed;

    /// <summary>
    /// Occurs when a pointer button is released on this node's hit path.
    /// </summary>
    public event EventHandler<UiPointerButtonEventArgs>? PointerReleased;

    /// <summary>
    /// Occurs when a pointer button press and release form a click on this node.
    /// </summary>
    public event EventHandler<UiPointerButtonEventArgs>? PointerClicked;

    /// <summary>
    /// Occurs when the pointer wheel moves through this node's hit path.
    /// </summary>
    public event EventHandler<UiPointerWheelEventArgs>? PointerWheel;

    /// <summary>
    /// Occurs when a key is pressed while this node has focus.
    /// </summary>
    public event EventHandler<UiKeyEventArgs>? KeyDown;

    /// <summary>
    /// Occurs when a key is released while this node has focus.
    /// </summary>
    public event EventHandler<UiKeyEventArgs>? KeyUp;

    /// <summary>
    /// Occurs when this node receives focus.
    /// </summary>
    public event EventHandler<UiFocusChangedEventArgs>? GotFocus;

    /// <summary>
    /// Occurs when this node loses focus.
    /// </summary>
    public event EventHandler<UiFocusChangedEventArgs>? LostFocus;

    /// <summary>
    /// Requests keyboard focus for this node.
    /// </summary>
    /// <returns>Whether this node became or remained focused.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the open screen is accessed from the wrong thread or a focus transition is already notifying.
    /// </exception>
    public bool Focus() =>
        Screen is not null && Screen.TryFocus(this);

    /// <summary>
    /// Converts a point from screen coordinates to this node's local coordinates.
    /// </summary>
    /// <param name="screenPoint">The point in screen coordinates.</param>
    /// <returns>The point in this node's local coordinates.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when this node does not belong to a UI screen or its open screen is accessed from the wrong thread.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when coordinate accumulation produces a non-finite value.
    /// </exception>
    public Point ScreenToLocal(Point screenPoint)
    {
        var screen = Screen;
        screen?.VerifyTreeAccess();
        if (screen is null)
            throw new InvalidOperationException("The UI node does not belong to a UI screen.");

        var point = screenPoint;
        for (var current = this; current is not null; current = current.Parent)
        {
            point = new Point(
                point.X - current.LayoutBounds.X,
                point.Y - current.LayoutBounds.Y);
        }

        return point;
    }

    /// <summary>
    /// Converts a point from this node's local coordinates to screen coordinates.
    /// </summary>
    /// <param name="localPoint">The point in this node's local coordinates.</param>
    /// <returns>The point in screen coordinates.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when this node does not belong to a UI screen or its open screen is accessed from the wrong thread.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when coordinate accumulation produces a non-finite value.
    /// </exception>
    public Point LocalToScreen(Point localPoint)
    {
        var screen = Screen;
        screen?.VerifyTreeAccess();
        if (screen is null)
            throw new InvalidOperationException("The UI node does not belong to a UI screen.");

        var point = localPoint;
        for (var current = this; current is not null; current = current.Parent)
        {
            point = new Point(
                point.X + current.LayoutBounds.X,
                point.Y + current.LayoutBounds.Y);
        }

        return point;
    }

    /// <summary>
    /// Raises the pointer entered event.
    /// </summary>
    /// <param name="eventArgs">The event data.</param>
    protected virtual void OnPointerEntered(UiPointerEventArgs eventArgs) =>
        PointerEntered?.Invoke(this, eventArgs);

    /// <summary>
    /// Raises the pointer exited event.
    /// </summary>
    /// <param name="eventArgs">The event data.</param>
    protected virtual void OnPointerExited(UiPointerEventArgs eventArgs) =>
        PointerExited?.Invoke(this, eventArgs);

    /// <summary>
    /// Raises the pointer moved event.
    /// </summary>
    /// <param name="eventArgs">The event data.</param>
    protected virtual void OnPointerMoved(UiPointerEventArgs eventArgs) =>
        PointerMoved?.Invoke(this, eventArgs);

    /// <summary>
    /// Raises the pointer pressed event.
    /// </summary>
    /// <param name="eventArgs">The event data.</param>
    protected virtual void OnPointerPressed(UiPointerButtonEventArgs eventArgs) =>
        PointerPressed?.Invoke(this, eventArgs);

    /// <summary>
    /// Raises the pointer released event.
    /// </summary>
    /// <param name="eventArgs">The event data.</param>
    protected virtual void OnPointerReleased(UiPointerButtonEventArgs eventArgs) =>
        PointerReleased?.Invoke(this, eventArgs);

    /// <summary>
    /// Raises the pointer clicked event.
    /// </summary>
    /// <param name="eventArgs">The event data.</param>
    protected virtual void OnPointerClicked(UiPointerButtonEventArgs eventArgs) =>
        PointerClicked?.Invoke(this, eventArgs);

    /// <summary>
    /// Raises the pointer wheel event.
    /// </summary>
    /// <param name="eventArgs">The event data.</param>
    protected virtual void OnPointerWheel(UiPointerWheelEventArgs eventArgs) =>
        PointerWheel?.Invoke(this, eventArgs);

    /// <summary>
    /// Raises the key down event.
    /// </summary>
    /// <param name="eventArgs">The event data.</param>
    protected virtual void OnKeyDown(UiKeyEventArgs eventArgs) =>
        KeyDown?.Invoke(this, eventArgs);

    /// <summary>
    /// Raises the key up event.
    /// </summary>
    /// <param name="eventArgs">The event data.</param>
    protected virtual void OnKeyUp(UiKeyEventArgs eventArgs) =>
        KeyUp?.Invoke(this, eventArgs);

    /// <summary>
    /// Raises the got focus event.
    /// </summary>
    /// <param name="eventArgs">The event data.</param>
    protected virtual void OnGotFocus(UiFocusChangedEventArgs eventArgs) =>
        GotFocus?.Invoke(this, eventArgs);

    /// <summary>
    /// Raises the lost focus event.
    /// </summary>
    /// <param name="eventArgs">The event data.</param>
    protected virtual void OnLostFocus(UiFocusChangedEventArgs eventArgs) =>
        LostFocus?.Invoke(this, eventArgs);

    internal void RaisePointerEntered(UiPointerEventArgs eventArgs) => OnPointerEntered(eventArgs);
    internal void RaisePointerExited(UiPointerEventArgs eventArgs) => OnPointerExited(eventArgs);
    internal void RaisePointerMoved(UiPointerEventArgs eventArgs) => OnPointerMoved(eventArgs);
    internal void RaisePointerPressed(UiPointerButtonEventArgs eventArgs) => OnPointerPressed(eventArgs);
    internal void RaisePointerReleased(UiPointerButtonEventArgs eventArgs) => OnPointerReleased(eventArgs);
    internal void RaisePointerClicked(UiPointerButtonEventArgs eventArgs) => OnPointerClicked(eventArgs);
    internal void RaisePointerWheel(UiPointerWheelEventArgs eventArgs) => OnPointerWheel(eventArgs);
    internal void RaiseKeyDown(UiKeyEventArgs eventArgs) => OnKeyDown(eventArgs);
    internal void RaiseKeyUp(UiKeyEventArgs eventArgs) => OnKeyUp(eventArgs);
    internal void RaiseGotFocus(UiFocusChangedEventArgs eventArgs) => OnGotFocus(eventArgs);
    internal void RaiseLostFocus(UiFocusChangedEventArgs eventArgs) => OnLostFocus(eventArgs);

    /// <summary>
    /// Determines whether a point lies within this node's local geometry.
    /// </summary>
    /// <param name="localPoint">The point in local coordinates.</param>
    /// <returns>Whether the point lies within this node's local geometry.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a node owned by an open screen is queried from the wrong thread.
    /// </exception>
    public bool Contains(Point localPoint)
    {
        Screen?.VerifyTreeAccess();
        return ContainsWithoutAccessCheck(localPoint);
    }

    /// <summary>
    /// Finds the frontmost deepest node at a point in this node's local coordinates.
    /// </summary>
    /// <param name="localPoint">The point in local coordinates.</param>
    /// <returns>The deepest hit node, or null when the subtree is not hit.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a tree owned by an open screen is queried from the wrong thread.
    /// </exception>
    public UiNode? HitTest(Point localPoint)
    {
        Screen?.VerifyTreeAccess();
        var path = new List<UiHitPathEntry>();
        return TryBuildHitPath(localPoint, path) ? path[^1].Node : null;
    }

    /// <summary>
    /// Determines whether a point lies within this node's local geometry.
    /// </summary>
    /// <param name="localPoint">The point in local coordinates.</param>
    /// <returns>Whether the point lies within this node's local geometry.</returns>
    protected virtual bool ContainsCore(Point localPoint) =>
        IsWithinLayoutBounds(localPoint);

    internal bool TryBuildHitPath(Point localPoint, List<UiHitPathEntry> path)
    {
        if (!_isHitTestLayoutValid || Visibility != Visibility.Visible || !IsHitTestVisible)
            return false;

        var originalCount = path.Count;
        path.Add(new UiHitPathEntry(this, localPoint));
        if (this is Parent parent &&
            (!parent.ClipToBounds || IsWithinLayoutBounds(localPoint)))
        {
            for (var index = parent.Children.Count - 1; index >= 0; index--)
            {
                var child = parent.Children[index];
                var childPoint = new Point(
                    localPoint.X - child.LayoutBounds.X,
                    localPoint.Y - child.LayoutBounds.Y);
                if (child.TryBuildHitPath(childPoint, path))
                    return true;
            }
        }

        if (ContainsForHitTest(localPoint))
            return true;

        path.RemoveRange(originalCount, path.Count - originalCount);
        return false;
    }

    private bool ContainsWithoutAccessCheck(Point localPoint) =>
        IsArrangeValid && ContainsCore(localPoint);

    private bool ContainsForHitTest(Point localPoint) =>
        _isHitTestLayoutValid && ContainsCore(localPoint);

    private bool IsWithinLayoutBounds(Point localPoint) =>
        localPoint.X >= 0 &&
        localPoint.Y >= 0 &&
        localPoint.X < LayoutBounds.Width &&
        localPoint.Y < LayoutBounds.Height;
}

internal readonly record struct UiHitPathEntry(UiNode Node, Point LocalPosition);
using PanguEngine.ComponentModel;
using PanguEngine.UI.Input;
using PanguEngine.UI.Styling;

namespace PanguEngine.UI;

public abstract partial class UIElement
{
    private static readonly PropertyKey<bool> IsHoveredPropertyKey =
        Property.RegisterReadOnly<UIElement, bool>(
            nameof(IsHovered),
            onChanged: static (node, _, _) => node.SetPseudoClass(PseudoClass.Hover, node.IsHovered));

    private static readonly PropertyKey<bool> IsFocusedPropertyKey =
        Property.RegisterReadOnly<UIElement, bool>(
            nameof(IsFocused),
            onChanged: static (node, _, _) => node.SetPseudoClass(PseudoClass.Focus, node.IsFocused));

    private static readonly PropertyKey<bool> IsFocusWithinPropertyKey =
        Property.RegisterReadOnly<UIElement, bool>(
            nameof(IsFocusWithin),
            onChanged: static (node, _, _) => node.SetPseudoClass(PseudoClass.FocusWithin, node.IsFocusWithin));

    /// <summary>
    /// Identifies the <see cref="Focusable"/> property.
    /// </summary>
    public static readonly Property<bool> FocusableProperty =
        Property.Register<UIElement, bool>(
            nameof(Focusable));

    /// <summary>
    /// Identifies the <see cref="IsHitTestVisible"/> property.
    /// </summary>
    public static readonly Property<bool> IsHitTestVisibleProperty =
        Property.Register<UIElement, bool>(
            nameof(IsHitTestVisible),
            true);

    /// <summary>
    /// Identifies the <see cref="IsEnabled"/> property.
    /// </summary>
    public static readonly Property<bool> IsEnabledProperty =
        Property.Register<UIElement, bool>(
            nameof(IsEnabled),
            true,
            onChanged: static (node, _, newValue) =>
            {
                node.SetPseudoClass(PseudoClass.Disabled, !node.IsEnabled);
                if (!newValue)
                    node.Scene?.CommitAndNotifyInputStateAfterNodeUnavailable(node);
            });

    /// <summary>
    /// Identifies the <see cref="IsHovered"/> property.
    /// </summary>
    public static readonly Property<bool> IsHoveredProperty = IsHoveredPropertyKey.Property;

    /// <summary>
    /// Identifies the <see cref="IsFocused"/> property.
    /// </summary>
    public static readonly Property<bool> IsFocusedProperty = IsFocusedPropertyKey.Property;

    /// <summary>
    /// Identifies the <see cref="IsFocusWithin"/> property.
    /// </summary>
    public static readonly Property<bool> IsFocusWithinProperty = IsFocusWithinPropertyKey.Property;

    /// <summary>
    /// Gets or sets whether this element can receive keyboard focus.
    /// </summary>
    public bool Focusable
    {
        get => GetValue(FocusableProperty);
        set => SetValue(FocusableProperty, value);
    }

    /// <summary>
    /// Gets or sets whether this element and its subtree participate in hit testing.
    /// </summary>
    public bool IsHitTestVisible
    {
        get => GetValue(IsHitTestVisibleProperty);
        set => SetValue(IsHitTestVisibleProperty, value);
    }

    /// <summary>
    /// Gets or sets whether this element and its subtree can receive input.
    /// </summary>
    public bool IsEnabled
    {
        get => GetValue(IsEnabledProperty);
        set => SetValue(IsEnabledProperty, value);
    }

    /// <summary>
    /// Gets whether the pointer currently targets this element or its subtree.
    /// </summary>
    public bool IsHovered => GetValue(IsHoveredProperty);

    /// <summary>
    /// Gets whether this element currently owns keyboard focus.
    /// </summary>
    public bool IsFocused => GetValue(IsFocusedProperty);

    /// <summary>
    /// Gets whether this element or one of its descendants currently owns keyboard focus.
    /// </summary>
    public bool IsFocusWithin => GetValue(IsFocusWithinProperty);

    /// <summary>
    /// Occurs when the pointer enters this element's hit path.
    /// </summary>
    public event EventHandler<PointerEventArgs>? PointerEntered;

    /// <summary>
    /// Occurs when the pointer leaves this element's hit path.
    /// </summary>
    public event EventHandler<PointerEventArgs>? PointerExited;

    /// <summary>
    /// Occurs when the pointer moves through this element's hit path.
    /// </summary>
    public event EventHandler<PointerEventArgs>? PointerMoved;

    /// <summary>
    /// Occurs when a pointer button is pressed on this element's hit path.
    /// </summary>
    public event EventHandler<PointerButtonEventArgs>? PointerPressed;

    /// <summary>
    /// Occurs when a pointer button is released on this element's hit path.
    /// </summary>
    public event EventHandler<PointerButtonEventArgs>? PointerReleased;

    /// <summary>
    /// Occurs when window focus loss cancels an active pointer button interaction on this element's route.
    /// </summary>
    /// <remarks>
    /// This event bubbles from the original pressed target after input state has been cleared.
    /// Each canceled button reports the last known pointer position and no key modifiers.
    /// Cancellation does not produce a release or click and does not require reverting interaction results.
    /// Scene closure and element unavailability use lifecycle cleanup without raising this event.
    /// </remarks>
    public event EventHandler<PointerButtonEventArgs>? PointerCanceled;

    /// <summary>
    /// Occurs when a pointer button press and release form a click on this element.
    /// </summary>
    public event EventHandler<PointerButtonEventArgs>? PointerClicked;

    /// <summary>
    /// Occurs when the pointer wheel moves through this element's hit path.
    /// </summary>
    public event EventHandler<PointerWheelEventArgs>? PointerWheel;

    /// <summary>
    /// Occurs when a key is pressed while this element has focus.
    /// </summary>
    public event EventHandler<KeyEventArgs>? KeyDown;

    /// <summary>
    /// Occurs when a key is released while this element has focus.
    /// </summary>
    public event EventHandler<KeyEventArgs>? KeyUp;

    /// <summary>
    /// Occurs when committed text is entered while this element has focus.
    /// </summary>
    public event EventHandler<TextInputEventArgs>? TextInput;

    /// <summary>
    /// Occurs when this element receives focus.
    /// </summary>
    public event EventHandler<FocusChangedEventArgs>? GotFocus;

    /// <summary>
    /// Occurs when this element loses focus.
    /// </summary>
    public event EventHandler<FocusChangedEventArgs>? LostFocus;

    /// <summary>
    /// Requests keyboard focus for this element.
    /// </summary>
    /// <returns>Whether the focus request was accepted.</returns>
    /// <remarks>Notification callbacks may make the resolved target unavailable before this method returns.</remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the open scene is accessed from the wrong thread or a focus transition is already notifying.
    /// </exception>
    public bool Focus() =>
        Scene is not null && Scene.TryFocus(this);

    /// <summary>Gets the element that receives a direct or pointer focus request for this element.</summary>
    /// <returns>The desired keyboard focus target.</returns>
    /// <remarks>
    /// The target is resolved once and must satisfy the current scene's focus eligibility rules.
    /// Overrides must not request focus, dispatch input, or modify the element tree.
    /// </remarks>
    protected internal virtual UIElement GetFocusTarget() => this;

    /// <summary>
    /// Converts a point from scene coordinates to this element's local coordinates.
    /// </summary>
    /// <param name="scenePoint">The point in scene coordinates.</param>
    /// <returns>The point in this element's local coordinates.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when this element does not belong to a UI scene or its open scene is accessed from the wrong thread.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when coordinate accumulation produces a non-finite value.
    /// </exception>
    public Point SceneToLocal(Point scenePoint)
    {
        var scene = Scene;
        scene?.VerifyTreeAccess();
        if (scene is null)
            throw new InvalidOperationException("The UI node does not belong to a UI scene.");

        var point = scenePoint;
        for (var current = this; current is not null; current = current.Parent)
        {
            point = new Point(
                point.X - current.LayoutBounds.X,
                point.Y - current.LayoutBounds.Y);
        }

        return point;
    }

    /// <summary>
    /// Converts a point from this element's local coordinates to scene coordinates.
    /// </summary>
    /// <param name="localPoint">The point in this element's local coordinates.</param>
    /// <returns>The point in scene coordinates.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when this element does not belong to a UI scene or its open scene is accessed from the wrong thread.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when coordinate accumulation produces a non-finite value.
    /// </exception>
    public Point LocalToScene(Point localPoint)
    {
        var scene = Scene;
        scene?.VerifyTreeAccess();
        if (scene is null)
            throw new InvalidOperationException("The UI node does not belong to a UI scene.");

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
    protected virtual void OnPointerEntered(PointerEventArgs eventArgs) =>
        PointerEntered?.Invoke(this, eventArgs);

    /// <summary>
    /// Raises the pointer exited event.
    /// </summary>
    /// <param name="eventArgs">The event data.</param>
    protected virtual void OnPointerExited(PointerEventArgs eventArgs) =>
        PointerExited?.Invoke(this, eventArgs);

    /// <summary>
    /// Raises the pointer moved event.
    /// </summary>
    /// <param name="eventArgs">The event data.</param>
    protected virtual void OnPointerMoved(PointerEventArgs eventArgs) =>
        PointerMoved?.Invoke(this, eventArgs);

    /// <summary>
    /// Raises the pointer pressed event.
    /// </summary>
    /// <param name="eventArgs">The event data.</param>
    protected virtual void OnPointerPressed(PointerButtonEventArgs eventArgs) =>
        PointerPressed?.Invoke(this, eventArgs);

    /// <summary>
    /// Raises the pointer released event.
    /// </summary>
    /// <param name="eventArgs">The event data.</param>
    protected virtual void OnPointerReleased(PointerButtonEventArgs eventArgs) =>
        PointerReleased?.Invoke(this, eventArgs);

    /// <summary>
    /// Raises the pointer canceled event.
    /// </summary>
    /// <param name="eventArgs">The event data.</param>
    protected virtual void OnPointerCanceled(PointerButtonEventArgs eventArgs) =>
        PointerCanceled?.Invoke(this, eventArgs);

    /// <summary>
    /// Raises the pointer clicked event.
    /// </summary>
    /// <param name="eventArgs">The event data.</param>
    protected virtual void OnPointerClicked(PointerButtonEventArgs eventArgs) =>
        PointerClicked?.Invoke(this, eventArgs);

    /// <summary>
    /// Raises the pointer wheel event.
    /// </summary>
    /// <param name="eventArgs">The event data.</param>
    protected virtual void OnPointerWheel(PointerWheelEventArgs eventArgs) =>
        PointerWheel?.Invoke(this, eventArgs);

    /// <summary>
    /// Raises the key down event.
    /// </summary>
    /// <param name="eventArgs">The event data.</param>
    protected virtual void OnKeyDown(KeyEventArgs eventArgs) =>
        KeyDown?.Invoke(this, eventArgs);

    /// <summary>
    /// Raises the key up event.
    /// </summary>
    /// <param name="eventArgs">The event data.</param>
    protected virtual void OnKeyUp(KeyEventArgs eventArgs) =>
        KeyUp?.Invoke(this, eventArgs);

    /// <summary>
    /// Raises the text input event.
    /// </summary>
    /// <param name="eventArgs">The event data.</param>
    protected virtual void OnTextInput(TextInputEventArgs eventArgs) =>
        TextInput?.Invoke(this, eventArgs);

    /// <summary>
    /// Raises the got focus event.
    /// </summary>
    /// <param name="eventArgs">The event data.</param>
    protected virtual void OnGotFocus(FocusChangedEventArgs eventArgs) =>
        GotFocus?.Invoke(this, eventArgs);

    /// <summary>
    /// Raises the lost focus event.
    /// </summary>
    /// <param name="eventArgs">The event data.</param>
    protected virtual void OnLostFocus(FocusChangedEventArgs eventArgs) =>
        LostFocus?.Invoke(this, eventArgs);

    internal void RaisePointerEntered(PointerEventArgs eventArgs) => OnPointerEntered(eventArgs);
    internal void RaisePointerExited(PointerEventArgs eventArgs) => OnPointerExited(eventArgs);
    internal void RaisePointerMoved(PointerEventArgs eventArgs) => OnPointerMoved(eventArgs);
    internal void RaisePointerPressed(PointerButtonEventArgs eventArgs) => OnPointerPressed(eventArgs);
    internal void RaisePointerReleased(PointerButtonEventArgs eventArgs) => OnPointerReleased(eventArgs);
    internal void RaisePointerCanceled(PointerButtonEventArgs eventArgs) => OnPointerCanceled(eventArgs);
    internal void RaisePointerClicked(PointerButtonEventArgs eventArgs) => OnPointerClicked(eventArgs);
    internal void RaisePointerWheel(PointerWheelEventArgs eventArgs) => OnPointerWheel(eventArgs);
    internal void RaiseKeyDown(KeyEventArgs eventArgs) => OnKeyDown(eventArgs);
    internal void RaiseKeyUp(KeyEventArgs eventArgs) => OnKeyUp(eventArgs);
    internal void RaiseTextInput(TextInputEventArgs eventArgs) => OnTextInput(eventArgs);
    internal void RaiseGotFocus(FocusChangedEventArgs eventArgs) => OnGotFocus(eventArgs);
    internal void RaiseLostFocus(FocusChangedEventArgs eventArgs) => OnLostFocus(eventArgs);

    internal void SetHovered(bool value)
    {
        if (value)
            SetValue(IsHoveredPropertyKey, true);
        else
            ClearValue(IsHoveredPropertyKey);
    }

    internal void SetFocused(bool value)
    {
        if (value)
            SetValue(IsFocusedPropertyKey, true);
        else
            ClearValue(IsFocusedPropertyKey);
    }

    internal void SetFocusWithin(bool value)
    {
        if (value)
            SetValue(IsFocusWithinPropertyKey, true);
        else
            ClearValue(IsFocusWithinPropertyKey);
    }

    /// <summary>
    /// Determines whether a point lies within this element's local geometry.
    /// </summary>
    /// <param name="localPoint">The point in local coordinates.</param>
    /// <returns>Whether the point lies within this element's local geometry.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when an element owned by an open scene is queried from the wrong thread.
    /// </exception>
    public bool Contains(Point localPoint)
    {
        Scene?.VerifyTreeAccess();
        return ContainsWithoutAccessCheck(localPoint);
    }

    /// <summary>
    /// Finds the frontmost deepest element at a point in this element's local coordinates.
    /// </summary>
    /// <param name="localPoint">The point in local coordinates.</param>
    /// <returns>The deepest hit element, or null when the subtree is not hit.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a tree owned by an open scene is queried from the wrong thread.
    /// </exception>
    public UIElement? HitTest(Point localPoint)
    {
        Scene?.VerifyTreeAccess();
        var path = new List<HitPathEntry>();
        return TryBuildHitPath(localPoint, path) ? path[^1].Node : null;
    }

    /// <summary>
    /// Determines whether a point lies within this element's local geometry.
    /// </summary>
    /// <param name="localPoint">The point in local coordinates.</param>
    /// <returns>Whether the point lies within this element's local geometry.</returns>
    protected virtual bool ContainsCore(Point localPoint) =>
        IsWithinLayoutBounds(localPoint);

    internal bool TryBuildHitPath(Point localPoint, List<HitPathEntry> path)
    {
        if (!_isHitTestLayoutValid ||
            Visibility != Visibility.Visible ||
            !IsHitTestVisible ||
            !IsEnabled)
            return false;

        var originalCount = path.Count;
        path.Add(new HitPathEntry(this, localPoint));
        if (this is Container parent &&
            (!parent.ClipToBounds || parent.ContainsDescendantClip(localPoint)))
        {
            for (var index = parent.ReadOnlyChildren.Count - 1; index >= 0; index--)
            {
                var child = parent.ReadOnlyChildren[index];
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

internal readonly record struct HitPathEntry(UIElement Node, Point LocalPosition);
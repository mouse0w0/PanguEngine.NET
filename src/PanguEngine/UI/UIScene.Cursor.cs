using PanguEngine.Windowing;

namespace PanguEngine.UI;

public partial class UIScene
{
    private UIElement? _pointerCursorTarget;
    private IDisposable? _pointerCursorSubscription;
    private Point? _pendingPointerPosition;

    /// <summary>Gets the pointer shape selected by the current hover target, or an arrow when none is available.</summary>
    public CursorShape PointerCursor { get; private set; } = CursorShape.Arrow;

    /// <summary>Occurs after the selected pointer shape changes.</summary>
    public event EventHandler? PointerCursorChanged;

    internal void SynchronizePointerPosition(Point position)
    {
        VerifyOwnerThread();
        if (!IsSceneActive())
            return;
        if (_isRoutingInput)
        {
            _pendingPointerPosition = position;
            return;
        }

        UpdatePointerPosition(position);
        RefreshPointerAfterLayout();
    }

    private void RefreshPointerCursor()
    {
        while (true)
        {
            if (IsUpdatingLayout) return;

            var target = GetPointerCursorTarget();
            if (!ReferenceEquals(target, _pointerCursorTarget))
            {
                _pointerCursorSubscription?.Dispose();
                _pointerCursorTarget = target;
                _pointerCursorSubscription = target?.Subscribe(UIElement.CursorProperty, (_, _) => RefreshPointerCursor());
            }

            var cursor = target?.Cursor ?? CursorShape.Arrow;
            if (PointerCursor == cursor) return;

            PointerCursor = cursor;
            PointerCursorChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private UIElement? GetPointerCursorTarget()
    {
        if (!IsSceneActive() || !_hasPointerPosition || _hoverPath.Count == 0)
            return null;

        var target = _hoverPath[^1].Node;
        if (!IsInputStateCurrent(target))
            return null;

        for (var node = target; node is not null; node = node.Parent)
        {
            if (!node.IsHitTestVisible)
                return null;
        }

        return target;
    }

    private bool CompleteInput(bool handled)
    {
        do
        {
            if (_pendingPointerPosition is { } position)
            {
                _pendingPointerPosition = null;
                if (IsSceneActive())
                    UpdateHover(UpdatePointerPosition(position));
            }

            RefreshPointerCursor();
        } while (_pendingPointerPosition is not null);

        return handled;
    }
}
